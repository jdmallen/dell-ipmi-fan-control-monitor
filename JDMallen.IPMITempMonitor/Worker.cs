using JDMallen.IPMITempMonitor.Services;
using JDMallen.Toolbox.Hosting;
using Microsoft.Extensions.Options;

namespace JDMallen.IPMITempMonitor;

/// <summary>
///     Main background service that orchestrates temperature monitoring and fan control.
///     Coordinates between TemperatureMonitor and FanController services to maintain
///     optimal server temperatures while minimizing fan noise.
/// </summary>
public class Worker(
	ILogger<Worker> logger,
	IOptions<Settings> settings,
	IServiceScopeFactory scopeFactory,
	ITemperatureMonitor temperatureMonitor,
	IFanController fanController)
	: ScopedBackgroundService<Worker>(logger, scopeFactory)
{
	private const string ISO8601_3_MILLIS = "yyyy-MM-ddTHH:mm:ss.fffK";

	private const string LOG_PREFIX =
		"[{DateTime}] Current temp: {LastRecordedTemp} C | Average temp: {RollingAverageTemp} C";

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

		LogInfo(
			"Fan control: {OperatingMode}",
			fanController.CurrentMode);

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

	private void Log(
		string str = "",
		LogLevel logLevel = LogLevel.Information,
		Exception? exception = null,
		params object[] addlArgs)
	{
		string message = string.IsNullOrWhiteSpace(str) ? LOG_PREFIX : LOG_PREFIX + " | " + str;
		double rollingAverageTemp = temperatureMonitor.RollingAverageTemperature;
		var args = new List<object>
		{
			DateTime.Now.ToString(ISO8601_3_MILLIS),
			temperatureMonitor.LastRecordedTemperature,
			rollingAverageTemp > 9000 ? "-" : rollingAverageTemp,
		};
		args.AddRange(addlArgs);
		_logger.Log(
			logLevel,
			exception,
			message,
			args.ToArray());
	}

	private void LogDebug(string str = "", params object[] addlArgs)
	{
		Log(str, LogLevel.Debug, addlArgs: addlArgs);
	}

	private void LogInfo(string str = "", params object[] addlArgs)
	{
		Log(str, addlArgs: addlArgs);
	}

	private void LogWarning(string str = "", params object[] addlArgs)
	{
		Log(str, LogLevel.Warning, addlArgs: addlArgs);
	}

	/// <summary>
	///     Triggered when the application host is ready to start the service.
	/// </summary>
	/// <param name="stoppingToken">
	///     Indicates that the start process has been aborted.
	/// </param>
	public override async Task StartAsync(CancellationToken stoppingToken)
	{
		LogDebug(
			"Detected OS {Os}",
			Settings.Platform.ToString("G"));

		await temperatureMonitor.CheckLatestTemperatureAsync(stoppingToken);

		LogInfo(
			"Monitor starting | Setting initial fan control to {OperatingMode}",
			OperatingMode.AUTOMATIC);

		await fanController.SwitchToAutomaticModeAsync(stoppingToken);

		await base.StartAsync(stoppingToken);
	}

	/// <summary>
	///     Triggered when the application host is performing a graceful shutdown.
	/// </summary>
	/// <param name="stoppingToken">Indicates that the shutdown process should no longer be graceful.</param>
	public override Task StopAsync(CancellationToken stoppingToken)
	{
		LogWarning("Monitor stopping");

		return base.StopAsync(stoppingToken);
	}
}
