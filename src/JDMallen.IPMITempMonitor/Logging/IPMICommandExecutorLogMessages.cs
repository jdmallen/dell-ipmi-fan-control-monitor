namespace JDMallen.IPMITempMonitor.Logging;

/// <summary>
///     High-performance logging messages for the IPMICommandExecutor service using LoggerMessage source generators.
/// </summary>
internal static partial class IPMICommandExecutorLogMessages
{
	[LoggerMessage(
		EventId = 100,
		Level = LogLevel.Debug,
		Message = "Executing: {IPMIPath} {Args}")]
	public static partial void LogExecutingCommand(
		this ILogger logger,
		string ipmiPath,
		string args);

	[LoggerMessage(
		EventId = 101,
		Level = LogLevel.Error,
		Message = "Unable to find test file; returning empty string")]
	public static partial void LogTestFileNotFound(
		this ILogger logger,
		Exception ex);

	[LoggerMessage(
		EventId = 102,
		Level = LogLevel.Error,
		Message = "Unknown error reading test file; returning empty string")]
	public static partial void LogUnknownErrorReadingTestFile(
		this ILogger logger,
		Exception ex);

	[LoggerMessage(
		EventId = 103,
		Level = LogLevel.Error,
		Message
			= "Process {Process} with args {Args} threw exception. Trying next of {Retries} attempt(s) after {Span} delay")]
	public static partial void LogProcessError(
		this ILogger logger,
		Exception exception,
		string process,
		string args,
		int retries,
		TimeSpan span);

	[LoggerMessage(
		EventId = 104,
		Level = LogLevel.Critical,
		Message = "Error calling ipmitool after {Retries} attempts!")]
	public static partial void LogCriticalIPMIFailure(
		this ILogger logger,
		Exception exception,
		int retries);
}
