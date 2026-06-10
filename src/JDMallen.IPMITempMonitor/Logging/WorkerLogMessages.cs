namespace JDMallen.IPMITempMonitor.Logging;

/// <summary>
///     High-performance logging messages for the Worker service using LoggerMessage source generators.
/// </summary>
internal static partial class WorkerLogMessages
{
	[LoggerMessage(
		EventId = 1,
		Level = LogLevel.Debug,
		Message
			= "[{DateTime}] Current temp: {LastRecordedTemp} C | Average temp: {RollingAverageTemp} C | Detected OS {Os}")]
	public static partial void LogDetectedOs(
		this ILogger logger,
		string dateTime,
		int lastRecordedTemp,
		object rollingAverageTemp,
		string os);

	[LoggerMessage(
		EventId = 2,
		Level = LogLevel.Information,
		Message
			= "[{DateTime}] Current temp: {LastRecordedTemp} C | Average temp: {RollingAverageTemp} C | Fan control: {OperatingMode}")]
	public static partial void LogFanControl(
		this ILogger logger,
		string dateTime,
		int lastRecordedTemp,
		object rollingAverageTemp,
		string operatingMode);

	[LoggerMessage(
		EventId = 3,
		Level = LogLevel.Information,
		Message
			= "[{DateTime}] Current temp: {LastRecordedTemp} C | Average temp: {RollingAverageTemp} C | Monitor starting | Setting initial fan control to {OperatingMode}")]
	public static partial void LogMonitorStarting(
		this ILogger logger,
		string dateTime,
		int lastRecordedTemp,
		object rollingAverageTemp,
		string operatingMode);

	[LoggerMessage(
		EventId = 4,
		Level = LogLevel.Warning,
		Message
			= "[{DateTime}] Current temp: {LastRecordedTemp} C | Average temp: {RollingAverageTemp} C | Monitor stopping")]
	public static partial void LogMonitorStopping(
		this ILogger logger,
		string dateTime,
		int lastRecordedTemp,
		object rollingAverageTemp);
}
