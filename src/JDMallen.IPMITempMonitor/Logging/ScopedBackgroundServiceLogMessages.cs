namespace JDMallen.IPMITempMonitor.Logging;

/// <summary>
///     High-performance logging messages for the ScopedBackgroundService base class using LoggerMessage source generators.
/// </summary>
internal static partial class ScopedBackgroundServiceLogMessages
{
	[LoggerMessage(
		EventId = 400,
		Level = LogLevel.Debug,
		Message = "Skipping execution iteration because previous iteration is still running")]
	public static partial void LogSkippingBusyIteration(this ILogger logger);

	[LoggerMessage(
		EventId = 401,
		Level = LogLevel.Trace,
		Message = "Begin service execution iteration")]
	public static partial void LogBeginIteration(this ILogger logger);

	[LoggerMessage(
		EventId = 402,
		Level = LogLevel.Trace,
		Message = "End service execution iteration")]
	public static partial void LogEndIteration(this ILogger logger);

	[LoggerMessage(
		EventId = 403,
		Level = LogLevel.Error,
		Message = "Unhandled exception in service execution iteration")]
	public static partial void LogUnhandledIterationException(
		this ILogger logger,
		Exception exception);
}
