namespace JDMallen.IPMITempMonitor.Hosting;

/// <summary>
///     Defines how a background service should handle overlapping executions when
///     work takes longer than the configured
///     <see cref="ScopedBackgroundService{TService}.LoopDelay" />.
/// </summary>
public enum OverlapBehavior
{
	/// <summary>
	///     Allow overlapping executions. New executions will start even if previous
	///     ones are still running. This is the default behavior.
	/// </summary>
	AllowOverlap = 0,

	/// <summary>
	///     Skip new executions if a previous execution is still running. The service
	///     will wait for the next scheduled interval before attempting to execute again.
	/// </summary>
	SkipIfBusy = 1,

	/// <summary>
	///     Wait for the previous execution to complete before starting a new one. This
	///     ensures executions never overlap but may cause drift in the execution schedule.
	/// </summary>
	WaitForCompletion = 2,
}
