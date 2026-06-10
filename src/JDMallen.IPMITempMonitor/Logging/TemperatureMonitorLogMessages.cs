namespace JDMallen.IPMITempMonitor.Logging;

/// <summary>
///     High-performance logging messages for the TemperatureMonitor service using LoggerMessage source generators.
/// </summary>
internal static partial class TemperatureMonitorLogMessages
{
	[LoggerMessage(
		EventId = 200,
		Level = LogLevel.Warning,
		Message
			= "Temperature check command returned empty result. Trying next of {Retries} attempt(s) after {Span} delay")]
	public static partial void LogEmptyTemperatureResult(
		this ILogger logger,
		int retries,
		TimeSpan span);

	[LoggerMessage(
		EventId = 201,
		Level = LogLevel.Error,
		Message = "Error fetching temperature after {Retries} attempts!")]
	public static partial void LogTemperatureFetchError(
		this ILogger logger,
		Exception? exception,
		int retries);
}
