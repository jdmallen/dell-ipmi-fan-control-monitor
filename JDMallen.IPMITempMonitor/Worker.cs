using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JDMallen.IPMITempMonitor
{
	public class Worker : BackgroundService
	{
		private readonly ILogger<Worker> _logger;
		private readonly Settings _settings;
		private static DateTime _timeFellBelowTemp = DateTime.MinValue;
		private static OperatingMode? _currentMode;
		private readonly IHostEnvironment _environment;
		private bool _belowTemp;
		private readonly List<int> _lastTenTemps;

		private const string CheckTemperatureControlCommand =
			"sdr type temperature";

		private const string EnableAutomaticTempControlCommand =
			"raw 0x30 0x30 0x01 0x01";

		private const string DisableAutomaticTempControlCommand =
			"raw 0x30 0x30 0x01 0x00";

		private const string StaticFanSpeedFormatString =
			"raw 0x30 0x30 0x02 0xff 0x{0}";

		public Worker(
			ILogger<Worker> logger,
			IOptions<Settings> settings,
			IHostEnvironment environment)
		{
			_logger = logger;
			_environment = environment;
			_settings = settings.Value;
			_lastTenTemps =
				new List<int>(_settings.RollingAverageNumberOfTemps);
		}

		protected override async Task ExecuteAsync(
			CancellationToken cancellationToken)
		{
			_logger.LogInformation($"Detected OS: {_settings.Platform:G}.");

			while (!cancellationToken.IsCancellationRequested)
			{
				var temp = await CheckLatestTemperature(cancellationToken);
				PushTemperature(temp);
				var rollingAverageTemp = GetRollingAverageTemperature();

				_logger.LogInformation(
					"Server fan control is {operatingMode}, temp is {temp} C, rolling average temp is {rollingAverageTemp} at {time}",
					_currentMode,
					temp,
					rollingAverageTemp,
					DateTimeOffset.Now);

				// If the temp goes above the max threshold,
				// immediately switch to Automatic fan mode.
				if (temp > _settings.MaxTempInC
				    || rollingAverageTemp > _settings.MaxTempInC)
				{
					_belowTemp = false;
					if (_currentMode == OperatingMode.Automatic)
					{
						await Delay(cancellationToken);
						continue;
					}

					await SwitchToAutomaticTempControl(cancellationToken);
				}
				// Only switch back to manual if both the current temp
				// AND the rolling average are back below the set max.
				else
				{
					if (!_belowTemp)
					{
						// Record the first record of when the temp dipped
						// below the max temp threshold. This is an extra
						// safety measure to ensure that Automatic mode isn't
						// turned off too soon. You can see its usage in
						// SwitchToManualTempControl().
						_timeFellBelowTemp = DateTime.UtcNow;
					}

					_belowTemp = true;

					if (_currentMode == OperatingMode.Manual)
					{
						await Delay(cancellationToken);
						continue;
					}

					await SwitchToManualTempControl(cancellationToken);
				}

				await Delay(cancellationToken);
			}
		}

		private void PushTemperature(int temp)
		{
			if (_lastTenTemps.Count == _settings.RollingAverageNumberOfTemps)
			{
				_lastTenTemps.RemoveAt(0);
			}

			_lastTenTemps.Add(temp);
		}

		private double GetRollingAverageTemperature()
		{
			return _lastTenTemps.Average();
		}

		private async Task Delay(
			CancellationToken cancellationToken)
		{
			await Task.Delay(
				TimeSpan.FromSeconds(_settings.PollingIntervalInSeconds),
				cancellationToken);
		}

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
		private async Task<int> CheckLatestTemperature(
			CancellationToken cancellationToken)
		{
			// Get the output string like the one in <remarks> above.
			var result =
				await ExecuteIpmiToolCommand(
					CheckTemperatureControlCommand,
					cancellationToken);

			// Using the default of (?<=0Eh|0Fh).+(\\d{2}) will return
			// all 2-digit numbers in lines containing "0Eh" or "0Fh"--
			// in this case, 30 and 31-- as captured groups.
			var matches = Regex.Matches(
				result,
				_settings.RegexToRetrieveTemp,
				RegexOptions.Multiline);

			// For each matched line, grab the last capture group (the 2-digit
			// temp) and attempt to convert it to an integer. Find the max
			// int of all the matched lines and return it.
			var maxCpuTemp = matches
				.Select(
					x => int.TryParse(
						x.Groups.Values.LastOrDefault()?.Value,
						out var temp)
						? temp
						: 0)
				.Max();

			return maxCpuTemp;
		}

		private async Task SwitchToAutomaticTempControl(
			CancellationToken cancellationToken)
		{
			_logger.LogInformation("Attempting switch to automatic mode.");
			await ExecuteIpmiToolCommand(
				EnableAutomaticTempControlCommand,
				cancellationToken);
			_currentMode = OperatingMode.Automatic;
		}

		private async Task SwitchToManualTempControl(
			CancellationToken cancellationToken)
		{
			var timeSinceLastActivation =
				DateTime.UtcNow - _timeFellBelowTemp;

			var threshold =
				TimeSpan.FromSeconds(_settings.BackToManualThresholdInSeconds);

			if (timeSinceLastActivation < threshold)
			{
				_logger.LogWarning(
					"Manual threshold not crossed yet. Staying in Automatic mode for at least another "
					+ $"{(int) (threshold - timeSinceLastActivation).TotalSeconds} seconds.");
				return;
			}

			_logger.LogInformation("Attempting switch to manual mode.");

			await ExecuteIpmiToolCommand(
				DisableAutomaticTempControlCommand,
				cancellationToken);

			var fanSpeedCommand = string.Format(
				StaticFanSpeedFormatString,
				_settings.ManualModeFanPercentage.ToString("X"));

			await ExecuteIpmiToolCommand(fanSpeedCommand, cancellationToken);
			_currentMode = OperatingMode.Manual;
		}

		private async Task<string> ExecuteIpmiToolCommand(
			string command,
			CancellationToken cancellationToken)
		{
			// Uses default path for either Linux or Windows,
			// unless a path is explicitly provided in appsettings.json.
			var ipmiPath =
				string.IsNullOrWhiteSpace(_settings.PathToIpmiToolIfNotDefault)
					? _settings.Platform switch
					{
						Platform.Linux => "/usr/bin/ipmitool",
						Platform.Windows =>
						@"C:\Program Files (x86)\Dell\SysMgt\bmc\ipmitool.exe",
						_ => throw new ArgumentOutOfRangeException()
					}
					: _settings.PathToIpmiToolIfNotDefault;

			var args =
				$"-I lanplus -H {_settings.IpmiHost} -U {_settings.IpmiUser} -P {_settings.IpmiPassword} {command}";

			_logger.LogDebug(
				$"Executing:\r\n{ipmiPath} {args.Replace(_settings.IpmiPassword, "{password}")}");

			string result;
			if (_environment.IsDevelopment())
			{
				// Your IPMI results may differ from my sample.
				result = await File.ReadAllTextAsync(
					Path.Combine(Environment.CurrentDirectory, "testdata.txt"),
					cancellationToken);
			}
			else
			{
				result = await RunProcess(ipmiPath, args, cancellationToken);
			}

			return result;
		}

		private async Task<string> RunProcess(
			string path,
			string args,
			CancellationToken cancellationToken)
		{
			string result = null;
			var process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = path,
					Arguments = args,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true
				}
			};

			try
			{
				process.Start();
				process.WaitForExit();
				result = await process.StandardOutput.ReadToEndAsync();
			}
			catch (Exception ex)
			{
				_logger.LogCritical(ex, "Error attempting to call ipmitool!");
				await StopAsync(cancellationToken);
			}

			return result;
		}

		/// <summary>
		/// Triggered when the application host is ready to start the service.
		/// </summary>
		/// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
		public override Task StartAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation(
				"Monitor starting. Setting fan control to automatic to start.");

			_currentMode = OperatingMode.Automatic;
#pragma warning disable 4014
			SwitchToAutomaticTempControl(cancellationToken);
#pragma warning restore 4014
			return base.StartAsync(cancellationToken);
		}

		/// <summary>
		/// Triggered when the application host is performing a graceful shutdown.
		/// </summary>
		/// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
		public override Task StopAsync(CancellationToken cancellationToken)
		{
			_logger.LogWarning("Monitor stopping");
			return base.StopAsync(cancellationToken);
		}
	}

	public enum OperatingMode
	{
		Automatic,
		Manual
	}
}
