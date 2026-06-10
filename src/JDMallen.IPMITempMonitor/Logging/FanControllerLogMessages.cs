namespace JDMallen.IPMITempMonitor.Logging;

/// <summary>
///     High-performance logging messages for the FanController service using LoggerMessage source generators.
/// </summary>
internal static partial class FanControllerLogMessages
{
	[LoggerMessage(
		EventId = 300,
		Level = LogLevel.Warning,
		Message = "Switching to {NewOperatingMode} fan control")]
	public static partial void LogSwitchingToAutomatic(
		this ILogger logger,
		string newOperatingMode);

	[LoggerMessage(
		EventId = 301,
		Level = LogLevel.Information,
		Message
			= "Switching to {NewOperatingMode} fan control | Attempt {AttemptNumber} of {TotalAttemptCount}")]
	public static partial void LogSwitchingToManual(
		this ILogger logger,
		string newOperatingMode,
		int attemptNumber,
		int totalAttemptCount);

	[LoggerMessage(
		EventId = 302,
		Level = LogLevel.Warning,
		Message
			= "{NewOperatingMode} delay threshold not yet met; staying in {OperatingMode} mode for {Remaining} {Unit}")]
	public static partial void LogManualDelayThresholdNotMet(
		this ILogger logger,
		string newOperatingMode,
		string operatingMode,
		int remaining,
		string unit);
}
