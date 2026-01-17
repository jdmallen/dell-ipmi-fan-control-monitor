using System.Diagnostics;
using JDMallen.IPMITempMonitor.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Contrib.WaitAndRetry;

namespace JDMallen.IPMITempMonitor.Services;

/// <summary>
///     Executes IPMI commands through ipmitool with automatic retry handling using Polly.
///     Supports both development mode (mocked responses) and production mode (actual IPMI calls).
/// </summary>
public class IPMICommandExecutor(
	ILogger<IPMICommandExecutor> logger,
	IOptions<Settings> settings,
	IConfiguration configuration,
	IHostEnvironment environment,
	IHostApplicationLifetime applicationLifetime) : IIPMICommandExecutor
{
	private const string CHECK_TEMPERATURE_CONTROL_COMMAND = "sdr type temperature";
	private const string DOCKER_ENV_VAR = "DOTNET_RUNNING_IN_CONTAINER";
	private readonly Settings _settings = settings.Value;

	/// <summary>
	///     Executes an IPMI command with exponential backoff retry policy.
	/// </summary>
	public async Task<string> ExecuteCommandAsync(string command, CancellationToken cancellationToken)
	{
		// Uses default path for either Linux or Windows,
		// unless a path is explicitly provided in appsettings.json.
		string ipmiPath =
			string.IsNullOrWhiteSpace(_settings.PathToIPMIToolIfNotDefault)
				? Settings.Platform switch
				{
					Platform.Linux   => "/usr/bin/ipmitool",
					Platform.Windows => @"C:\Program Files (x86)\Dell\SysMgt\bmc\ipmitool.exe",
					_                => throw new ArgumentOutOfRangeException(),
				}
				: _settings.PathToIPMIToolIfNotDefault;

		string args =
			$"-I lanplus -H {_settings.IPMIHost} -U {_settings.IPMIUser} "
			+ $"-P {_settings.IPMIPassword} {command}";

		logger.LogExecutingCommand(
			ipmiPath,
			args.Replace(_settings.IPMIPassword, "<password>"));

		if (environment.IsDevelopment())
		{
			return command switch
			{
				// Your IPMI results may differ from my sample.
				CHECK_TEMPERATURE_CONTROL_COMMAND => await ReadTestResponseFile(cancellationToken),
				_                                 => string.Empty,
			};
		}

		return await RunProcessAsync(ipmiPath, args, cancellationToken);
	}

	/// <summary>
	///     Reads test response file for development mode testing.
	/// </summary>
	private async Task<string> ReadTestResponseFile(CancellationToken cancellationToken)
	{
		const string filename = "test_temp_response.txt";
		try
		{
			// If not in docker, use the file from the source directory.
			var isInDocker = configuration.GetValue<bool>(DOCKER_ENV_VAR);
			string path = isInDocker
				? Path.Combine(AppContext.BaseDirectory, filename)
				: Path.Combine(
					AppContext.BaseDirectory,
					$"..{Path.DirectorySeparatorChar}",
					$"..{Path.DirectorySeparatorChar}",
					$"..{Path.DirectorySeparatorChar}",
					filename
				);

			return await File.ReadAllTextAsync(path, cancellationToken);
		}
		catch (FileNotFoundException ex)
		{
			logger.LogTestFileNotFound(ex);

			return string.Empty;
		}
		catch (Exception ex)
		{
			logger.LogUnknownErrorReadingTestFile(ex);

			return string.Empty;
		}
	}

	/// <summary>
	///     Runs an external process with retry logic using Polly exponential backoff.
	/// </summary>
	private async Task<string> RunProcessAsync(
		string path,
		string args,
		CancellationToken cancellationToken)
	{
		int retryCount = _settings.PollyRetryOnFailureCount;

		IEnumerable<TimeSpan> delay = Backoff.ExponentialBackoff(
			TimeSpan.FromMilliseconds(_settings.PollyInitialDelayInMillis),
			retryCount,
			_settings.PollyDelayIncreaseFactor);

		var process = new Process
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = path,
				Arguments = args,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			},
		};

		PolicyResult<string> policyExecutionResult = await Policy
			.Handle<Exception>()
			.WaitAndRetryAsync(
				delay,
				(exception, span, iteration, _) =>
				{
					logger.LogProcessError(
						exception,
						process.StartInfo.FileName,
						process.StartInfo.Arguments,
						retryCount - iteration + 1,
						span);
				})
			.ExecuteAndCaptureAsync(
				async token =>
				{
					process.Start();
					await process.WaitForExitAsync(token);

					return await process.StandardOutput.ReadToEndAsync();
				},
				cancellationToken);

		if (policyExecutionResult.Outcome != OutcomeType.Failure)
		{
			return policyExecutionResult.Result;
		}

		logger.LogCriticalIPMIFailure(
			policyExecutionResult.FinalException!,
			retryCount);

		// Critical failure - stop the application
		applicationLifetime.StopApplication();

		return policyExecutionResult.Result;
	}
}
