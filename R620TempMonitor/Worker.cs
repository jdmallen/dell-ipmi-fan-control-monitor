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

namespace R620TempMonitor
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
			CancellationToken stoppingToken)
		{
			_logger.LogInformation($"Detected OS: {_settings.Platform:G}.");

			while (!stoppingToken.IsCancellationRequested)
			{
				var temp = await CheckLatestTemperature();
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
				if (temp > _settings.MaxTempInC || rollingAverageTemp > _settings.MaxTempInC)
				{
					_belowTemp = false;
					if (_currentMode == OperatingMode.Automatic)
					{
						await Delay(stoppingToken);
						continue;
					}

					await SwitchToAutomaticTempControl();
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
						await Delay(stoppingToken);
						continue;
					}

					await SwitchToManualTempControl();
				}

				await Delay(stoppingToken);
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
			CancellationToken stoppingToken)
		{
			await Task.Delay(
				TimeSpan.FromSeconds(_settings.PollingIntervalInSeconds),
				stoppingToken);
		}

		/// <summary>
		/// Calls iDRAC for latest temperature. Ensure that the Regex setting
		/// to retrieve the temp has been updated for your particular system.
		/// Mine is set for an R620 system.
		/// </summary>
		/// <returns></returns>
		private async Task<int> CheckLatestTemperature()
		{
			var result =
				await ExecuteIpmiToolCommand("sdr type temperature");
			var temp = Regex.Match(
					result,
					_settings.RegexToRetrieveTemp,
					RegexOptions.Multiline)
				.Groups.Values.Last()
				.Value;
			int.TryParse(temp, out var intTemp);
			return intTemp;
		}

		private async Task SwitchToAutomaticTempControl()
		{
			_logger.LogInformation("Attempting switch to automatic mode.");
			await ExecuteIpmiToolCommand(EnableAutomaticTempControlCommand);
			_currentMode = OperatingMode.Automatic;
		}

		private async Task SwitchToManualTempControl()
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

			await ExecuteIpmiToolCommand(DisableAutomaticTempControlCommand);

			var fanSpeedCommand = string.Format(
				StaticFanSpeedFormatString,
				_settings.ManualModeFanPercentage.ToString("X"));

			await ExecuteIpmiToolCommand(fanSpeedCommand);
			_currentMode = OperatingMode.Manual;
		}

		private async Task<string> ExecuteIpmiToolCommand(string command)
		{
			// Uses default path for either Linux or Windows,
			// unless a path is explicitly provided in appsettings.json.
			var IpmiPath =
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
				$"Executing:\r\n{IpmiPath} {args.Replace(_settings.IpmiPassword, "{password}")}");

			string result;
			if (_environment.IsDevelopment())
			{
				// Your IPMI results may differ from my sample.
				result = await File.ReadAllTextAsync(
					Path.Combine(Environment.CurrentDirectory, "testdata.txt"));
			}
			else
			{
				var process = new Process
				{
					StartInfo = new ProcessStartInfo
					{
						FileName = IpmiPath,
						Arguments = args,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						UseShellExecute = false,
						CreateNoWindow = true
					}
				};

				process.Start();
				process.WaitForExit();
				result = await process.StandardOutput.ReadToEndAsync();
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
			SwitchToAutomaticTempControl();
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
