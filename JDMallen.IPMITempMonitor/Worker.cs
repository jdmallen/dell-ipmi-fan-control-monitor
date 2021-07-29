using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JDMallen.Toolbox.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Contrib.WaitAndRetry;

namespace JDMallen.IPMITempMonitor
{
	public class Worker : ScopedBackgroundService<Worker>
	{
		private readonly ILogger<Worker> _logger;
		private readonly Settings _settings;
		private readonly IConfiguration _configuration;
		private readonly IHostApplicationLifetime _applicationLifetime;
		private static DateTime _timeFellBelowTemp = DateTime.MinValue;
		private static OperatingMode? _currentMode;
		private readonly IHostEnvironment _environment;
		private bool _belowTemp;
		private int _manualSwitchAttemptCount;
		private readonly List<int> _lastTenTemps;
		private int _lastRecordedTemp;

		private const string CHECK_TEMPERATURE_CONTROL_COMMAND = "sdr type temperature";
		private const string ENABLE_AUTOMATIC_TEMP_CONTROL_COMMAND = "raw 0x30 0x30 0x01 0x01";
		private const string DISABLE_AUTOMATIC_TEMP_CONTROL_COMMAND = "raw 0x30 0x30 0x01 0x00";
		private const string STATIC_FAN_SPEED_FORMAT_STRING = "raw 0x30 0x30 0x02 0xff 0x{0}";

		private const string DOCKER_ENV_VAR = "DOTNET_RUNNING_IN_CONTAINER";
		private const string ISO8601_3_MILLIS = "yyyy-MM-ddTHH:mm:ss.fffK";

		public Worker(
			ILogger<Worker> logger,
			IOptions<Settings> settings,
			IHostEnvironment environment,
			IServiceScopeFactory scopeFactory,
			IConfiguration configuration,
			IHostApplicationLifetime applicationLifetime) : base(logger, scopeFactory)
		{
			_logger = logger;
			_environment = environment;
			_configuration = configuration;
			_applicationLifetime = applicationLifetime;
			_settings = settings.Value;
			_lastTenTemps = new List<int>(_settings.RollingAverageNumberOfTemps);
		}

		private const string LOG_PREFIX =
			"[{dateTime}] Current temp: {lastRecordedTemp} C | Average temp: {rollingAverageTemp} C";

		private void LogDebug(string str = "", params object[] addlArgs)
		{
			Log(str, LogLevel.Debug, addlArgs: addlArgs);
		}

		private void LogInfo(string str = "", params object[] addlArgs)
		{
			Log(str, LogLevel.Information, addlArgs: addlArgs);
		}

		private void LogWarning(string str = "", params object[] addlArgs)
		{
			Log(str, LogLevel.Warning, addlArgs: addlArgs);
		}

		private void LogError(Exception exception = null, string str = "", params object[] addlArgs)
		{
			Log(
				str,
				LogLevel.Warning,
				exception,
				addlArgs);
		}

		private void LogCritical(
			Exception exception = null,
			string str = "",
			params object[] addlArgs)
		{
			Log(
				str,
				LogLevel.Critical,
				exception,
				addlArgs);
		}

		private void Log(
			string str = "",
			LogLevel logLevel = LogLevel.Information,
			Exception exception = null,
			params object[] addlArgs)
		{
			string message = string.IsNullOrWhiteSpace(str) ? LOG_PREFIX : LOG_PREFIX + " | " + str;
			double rollingAverageTemp = GetRollingAverageTemperature();
			var args = new List<object>
			{
				DateTime.Now.ToString(ISO8601_3_MILLIS),
				_lastRecordedTemp,
				rollingAverageTemp > 9000 ? "-" : rollingAverageTemp,
			};
			args.AddRange(addlArgs);
			_logger.Log(
				logLevel,
				exception,
				message,
				args.ToArray());
		}

		protected override async Task ExecuteInScopeAsync(
			IServiceScope scope,
			CancellationToken stoppingToken)
		{
			await CheckLatestTemperature(stoppingToken);
			double rollingAverageTemp = GetRollingAverageTemperature();

			LogInfo(
				"Fan control: {operatingMode}",
				_currentMode ?? OperatingMode.UNKNOWN);

			// If the temp goes above the max threshold, immediately switch to AUTOMATIC fan mode.
			if (_lastRecordedTemp > _settings.MaxTempInC
				|| rollingAverageTemp > _settings.MaxTempInC)
			{
				_belowTemp = false;
				if (_currentMode == OperatingMode.AUTOMATIC)
				{
					return;
				}

				await SwitchToAutomaticTempControl(stoppingToken);

				return;
			}

			// Only switch back to manual if both the current temp AND the rolling average are back
			// below the set max.

			if (!_belowTemp)
			{
				// Record the first record of when the temp dipped below the max temp threshold.
				// This is an extra safety measure to ensure that AUTOMATIC mode isn't turned off
				// too soon. You can see its usage in SwitchToManualTempControl.
				_timeFellBelowTemp = DateTime.UtcNow;

				// Reset the number of times we attempt to switch to MANUAL, based on setting
				// (default: 2). Note that if the temperature goes above threshold between attempts,
				// subsequent attempts are skipped and it switches to AUTOMATIC fan control mode
				// (as one would hope).
				_manualSwitchAttemptCount = _settings.ManualModeSwitchReattempts;
			}

			_belowTemp = true;

			if (_currentMode == OperatingMode.MANUAL && _manualSwitchAttemptCount == 0)
			{
				return;
			}

			await SwitchToManualTempControl(stoppingToken);
		}

		protected override TimeSpan LoopDelay
			=> TimeSpan.FromSeconds(_settings.PollingIntervalInSeconds);

		private void PushTemperature(int temp)
		{
			if (_lastTenTemps.Count == _settings.RollingAverageNumberOfTemps)
			{
				_lastTenTemps.RemoveAt(0);
			}

			_lastTenTemps.Add(temp);
		}

		private double GetRollingAverageTemperature() => Math.Round(
			_lastTenTemps != null && _lastTenTemps.Any() ? _lastTenTemps.Average() : 9999,
			1);

		/// <summary>
		/// Calls iDRAC for latest temperature.
		/// </summary>
		/// <remarks>
		/// Ensure that the Regex setting to retrieve the temp(s) has been
		/// updated for your particular system. Mine is set for an R620 system.
		/// The default values provided in this project are meant to parse an
		/// output like the below. The inline comments will reference this
		/// as an example:
		///     Inlet Temp       | 04h | ok  |  7.1 | 20 degrees C
		///     Exhaust Temp     | 01h | ok  |  7.1 | 25 degrees C
		///     Temp             | 0Eh | ok  |  3.1 | 30 degrees C
		///     Temp             | 0Fh | ok  |  3.2 | 31 degrees C
		/// </remarks>
		/// <returns></returns>
		private async Task CheckLatestTemperature(CancellationToken cancellationToken)
		{
			// Get the output string like the one in <remarks> above. Using Polly to handle if/when
			// the result is empty, which can happen from time to time.
			int retryCount = _settings.PollyRetryOnFailureCount;

			IEnumerable<TimeSpan> delay = Backoff.ExponentialBackoff(
				TimeSpan.FromMilliseconds(_settings.PollyInitialDelayInMillis),
				retryCount,
				_settings.PollyDelayIncreaseFactor);

			PolicyResult<string> policyExecutionResult = await Policy
				.HandleResult<string>(string.IsNullOrWhiteSpace)
				.WaitAndRetryAsync(
					delay,
					(_, span, iteration, _) =>
					{
						LogWarning(
							"Temperature check command returned empty result. "
							+ "Trying next of {retries} attempt(s) after {span} delay.",
							retryCount - iteration + 1,
							span);
					})
				.ExecuteAndCaptureAsync(
					async token => await ExecuteIpmiToolCommand(
						CHECK_TEMPERATURE_CONTROL_COMMAND,
						token),
					cancellationToken);

			if (policyExecutionResult.Outcome == OutcomeType.Failure)
			{
				LogError(
					policyExecutionResult.FinalException,
					"Error fetching temperature after {retries} attempts!",
					retryCount);

				return;
			}

			string result = policyExecutionResult.Result;

			// Using the default of (?<=0Eh|0Fh).+(\\d{2}) will return all 2-digit numbers in lines
			// containing "0Eh" or "0Fh"-- in the above example, 30 and 31-- as captured groups.
			MatchCollection matches = Regex.Matches(
				result,
				_settings.RegexToRetrieveTemp,
				RegexOptions.Multiline);

			if (!matches.Any())
			{
				return;
			}

			// For each matched line, grab the last capture group (the 2-digit
			// temp) and attempt to convert it to an integer. Find the max
			// int of all the matched lines and return it.
			int maxCpuTemp = matches.Select(
					x => int.TryParse(x.Groups.Values.LastOrDefault()?.Value, out int temp)
						? temp
						: 0)
				.Max();

			PushTemperature(maxCpuTemp);

			_lastRecordedTemp = maxCpuTemp;
		}

		private async Task SwitchToAutomaticTempControl(CancellationToken stoppingToken)
		{
			LogWarning(
				"Switching to {newOperatingMode} fan control",
				OperatingMode.AUTOMATIC);

			await ExecuteIpmiToolCommand(
				ENABLE_AUTOMATIC_TEMP_CONTROL_COMMAND,
				stoppingToken);

			_currentMode = OperatingMode.AUTOMATIC;
		}

		private async Task SwitchToManualTempControl(CancellationToken stoppingToken)
		{
			TimeSpan timeSinceLastActivation = DateTime.UtcNow - _timeFellBelowTemp;

			TimeSpan threshold = TimeSpan.FromSeconds(_settings.BackToManualThresholdInSeconds);

			if (timeSinceLastActivation < threshold)
			{
				var secondsRemaining =
					(int) (threshold - timeSinceLastActivation).TotalSeconds;

				LogWarning(
					"{newOperatingMode} delay threshold not yet met; "
					+ "staying in {operatingMode} mode for {remaining} {unit}.",
					OperatingMode.MANUAL,
					OperatingMode.AUTOMATIC,
					secondsRemaining,
					secondsRemaining == 1 ? "second" : "seconds");

				return;
			}

			LogInfo(
				"Switching to {newOperatingMode} fan control | "
				+ "Attempt {attemptNumber} of {totalAttemptCount}",
				OperatingMode.MANUAL,
				_settings.ManualModeSwitchReattempts - _manualSwitchAttemptCount + 1,
				_settings.ManualModeSwitchReattempts);

			await ExecuteIpmiToolCommand(
				DISABLE_AUTOMATIC_TEMP_CONTROL_COMMAND,
				stoppingToken);

			string fanSpeedCommand = string.Format(
				STATIC_FAN_SPEED_FORMAT_STRING,
				_settings.ManualModeFanPercentage.ToString("X"));

			await ExecuteIpmiToolCommand(fanSpeedCommand, stoppingToken);
			_currentMode = OperatingMode.MANUAL;
			if (_manualSwitchAttemptCount >= 1)
			{
				_manualSwitchAttemptCount--;
			}
		}

		private async Task<string> ExecuteIpmiToolCommand(
			string command,
			CancellationToken stoppingToken)
		{
			// Uses default path for either Linux or Windows,
			// unless a path is explicitly provided in appsettings.json.
			string ipmiPath =
				string.IsNullOrWhiteSpace(_settings.PathToIpmiToolIfNotDefault)
					? Settings.Platform switch
					{
						Platform.Linux   => "/usr/bin/ipmitool",
						Platform.Windows => @"C:\Program Files (x86)\Dell\SysMgt\bmc\ipmitool.exe",
						_                => throw new ArgumentOutOfRangeException(),
					}
					: _settings.PathToIpmiToolIfNotDefault;

			string args =
				$"-I lanplus -H {_settings.IpmiHost} -U {_settings.IpmiUser} "
				+ $"-P {_settings.IpmiPassword} {command}";

			LogDebug(
				"Executing: {ipmiPath} {args}",
				ipmiPath,
				args.Replace(_settings.IpmiPassword, "<password>"));

			if (_environment.IsDevelopment())
			{
				return command switch
				{
					// Your IPMI results may differ from my sample.
					CHECK_TEMPERATURE_CONTROL_COMMAND => await ReadTestResponseFile(stoppingToken),
					_                                 => string.Empty,
				};
			}

			return await RunProcess(ipmiPath, args, stoppingToken);
		}

		private async Task<string> RunProcess(
			string path,
			string args,
			CancellationToken stoppingToken)
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
						LogError(
							exception,
							"Process {process} with args {args} threw exception. "
							+ "Trying next of {retries} attempt(s) after {span} delay.",
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
					stoppingToken);

			if (policyExecutionResult.Outcome != OutcomeType.Failure)
			{
				return policyExecutionResult.Result;
			}

			LogCritical(
				policyExecutionResult.FinalException,
				"Error calling ipmitool after {retries} attempts!",
				retryCount);
			await StopAsync(stoppingToken);
			_applicationLifetime.StopApplication();

			return policyExecutionResult.Result;
		}

		private async Task<string> ReadTestResponseFile(CancellationToken stoppingToken)
		{
			const string filename = "test_temp_response.txt";
			try
			{
				// If not in docker, use the file from the source directory.
				var isInDocker = _configuration.GetValue<bool>(DOCKER_ENV_VAR);
				var path = isInDocker
					? Path.Combine(AppContext.BaseDirectory, filename)
					: Path.Combine(
						AppContext.BaseDirectory,
						$"..{Path.DirectorySeparatorChar}",
						$"..{Path.DirectorySeparatorChar}",
						$"..{Path.DirectorySeparatorChar}",
						filename
					);

				return await File.ReadAllTextAsync(path, stoppingToken);
			}
			catch (FileNotFoundException ex)
			{
				LogError(ex, "Unable to find test file; returning empty string.");

				return string.Empty;
			}
			catch (Exception ex)
			{
				LogError(ex, "Unknown error reading test file; returning empty string.");

				return string.Empty;
			}
		}

		/// <summary>
		/// Triggered when the application host is ready to start the service.
		/// </summary>
		/// <param name="stoppingToken">
		/// Indicates that the start process has been aborted.
		/// </param>
		public override async Task StartAsync(CancellationToken stoppingToken)
		{
			var isInDocker = _configuration.GetValue<bool>(DOCKER_ENV_VAR);
			LogDebug(
				"Detected OS {os}",
				Settings.Platform.ToString("G") + (isInDocker ? " (Docker container)" : ""));

			await CheckLatestTemperature(stoppingToken);

			LogInfo(
				"Monitor starting | Setting initial fan control to {operatingMode}",
				OperatingMode.AUTOMATIC);

			_currentMode = OperatingMode.AUTOMATIC;

			await SwitchToAutomaticTempControl(stoppingToken);

			await base.StartAsync(stoppingToken);
		}

		/// <summary>
		/// Triggered when the application host is performing a graceful shutdown.
		/// </summary>
		/// <param name="stoppingToken">Indicates that the shutdown process should no longer be graceful.</param>
		public override Task StopAsync(CancellationToken stoppingToken)
		{
			LogWarning("Monitor stopping");

			return base.StopAsync(stoppingToken);
		}
	}

	[SuppressMessage("ReSharper", "InconsistentNaming")]
	public enum OperatingMode
	{
		UNKNOWN,
		AUTOMATIC,
		MANUAL,
	}
}
