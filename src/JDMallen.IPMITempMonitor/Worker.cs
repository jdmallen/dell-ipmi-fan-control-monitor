using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using JDMallen.IPMITempMonitor.Logging;
using JDMallen.Toolbox.Hosting;
using JDMallen.IPMITempMonitor.Services;
using Microsoft.Extensions.Options;

namespace JDMallen.IPMITempMonitor;

/// <summary>
///     Main background service that orchestrates temperature monitoring and fan control.
///     Coordinates between TemperatureMonitor and FanController services to maintain
///     optimal server temperatures while minimizing fan noise.
/// </summary>
[SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging")]
public class Worker(
	ILogger<Worker> logger,
	IOptions<Settings> settings,
	IServiceScopeFactory scopeFactory,
	ITemperatureMonitor temperatureMonitor,
	IFanController fanController)
	: ScopedBackgroundService<Worker>(logger, scopeFactory)
{
	private const string ISO8601_3_MILLIS = "yyyy-MM-ddTHH:mm:ss.fffK";
	private readonly ILogger<Worker> _logger = logger;
	private readonly Settings _settings = settings.Value;
	private bool _belowTemp;

	protected override TimeSpan LoopDelay
		=> TimeSpan.FromSeconds(_settings.PollingIntervalInSeconds);

	protected override async Task ExecuteInScopeAsync(
		IServiceScope scope,
		CancellationToken stoppingToken)
	{
		await temperatureMonitor.CheckLatestTemperatureAsync(stoppingToken);
		double rollingAverageTemp = temperatureMonitor.RollingAverageTemperature;

		_logger.LogFanControl(
			DateTime.Now.ToString(ISO8601_3_MILLIS, CultureInfo.CurrentCulture),
			temperatureMonitor.LastRecordedTemperature,
			rollingAverageTemp > 9000 ? "-" : rollingAverageTemp,
			fanController.CurrentMode.ToString("G"));

		// If the temp goes above the max threshold, immediately switch to AUTOMATIC fan mode.
		if (temperatureMonitor.LastRecordedTemperature > _settings.MaxTempInC
		    || rollingAverageTemp > _settings.MaxTempInC)
		{
			_belowTemp = false;
			if (fanController.CurrentMode == OperatingMode.AUTOMATIC)
			{
				return;
			}

			await fanController.SwitchToAutomaticModeAsync(stoppingToken);

			return;
		}

		// Only switch back to manual if both the current temp AND the rolling average are back
		// below the set max.

		if (!_belowTemp)
		{
			fanController.RecordTemperatureBelowThreshold();
		}

		_belowTemp = true;

		if (!fanController.ShouldAttemptManualModeSwitch())
		{
			return;
		}

		await fanController.SwitchToManualModeAsync(stoppingToken);
	}

	/// <summary>
	///     Triggered when the application host is ready to start the service.
	/// </summary>
	/// <param name="cancellationToken">
	///     Indicates that the start process has been aborted.
	/// </param>
	public override async Task StartAsync(CancellationToken cancellationToken)
	{
		await temperatureMonitor.CheckLatestTemperatureAsync(cancellationToken);

		double rollingAverageTemp = temperatureMonitor.RollingAverageTemperature;

		_logger.LogDetectedOs(
			DateTime.Now.ToString(ISO8601_3_MILLIS, CultureInfo.CurrentCulture),
			temperatureMonitor.LastRecordedTemperature,
			rollingAverageTemp > 9000 ? "-" : rollingAverageTemp,
			Settings.Platform.ToString("G"));

		_logger.LogMonitorStarting(
			DateTime.Now.ToString(ISO8601_3_MILLIS, CultureInfo.CurrentCulture),
			temperatureMonitor.LastRecordedTemperature,
			rollingAverageTemp > 9000 ? "-" : rollingAverageTemp,
			OperatingMode.AUTOMATIC.ToString("G"));

		await fanController.SwitchToAutomaticModeAsync(cancellationToken);

		await base.StartAsync(cancellationToken);
	}

	/// <summary>
	///     Triggered when the application host is performing a graceful shutdown.
	/// </summary>
	/// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
	public override Task StopAsync(CancellationToken cancellationToken)
	{
		double rollingAverageTemp = temperatureMonitor.RollingAverageTemperature;

		_logger.LogMonitorStopping(
			DateTime.Now.ToString(ISO8601_3_MILLIS, CultureInfo.CurrentCulture),
			temperatureMonitor.LastRecordedTemperature,
			rollingAverageTemp > 9000 ? "-" : rollingAverageTemp);

		return base.StopAsync(cancellationToken);
	}
}
