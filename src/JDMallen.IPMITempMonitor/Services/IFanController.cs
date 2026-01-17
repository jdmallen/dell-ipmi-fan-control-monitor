namespace JDMallen.IPMITempMonitor.Services;

/// <summary>
///     Controls server fan operation mode between automatic and manual.
/// </summary>
public interface IFanController
{
	/// <summary>
	///     Gets the current operating mode of the fan controller.
	/// </summary>
	OperatingMode CurrentMode { get; }

	/// <summary>
	///     Records that the temperature has fallen below the threshold.
	///     Resets the retry counter for switching to manual mode.
	/// </summary>
	void RecordTemperatureBelowThreshold();

	/// <summary>
	///     Checks if the system should attempt to switch to manual mode.
	/// </summary>
	/// <returns>True if a switch attempt should be made</returns>
	bool ShouldAttemptManualModeSwitch();

	/// <summary>
	///     Switches the fan control to automatic mode (BIOS-controlled).
	/// </summary>
	/// <param name="cancellationToken">Token to cancel the operation</param>
	Task SwitchToAutomaticModeAsync(CancellationToken cancellationToken);

	/// <summary>
	///     Switches the fan control to manual mode with a fixed fan speed percentage.
	///     Implements retry logic based on configuration.
	/// </summary>
	/// <param name="cancellationToken">Token to cancel the operation</param>
	/// <returns>True if the switch was successful or skipped due to threshold; false if retries exhausted</returns>
	Task<bool> SwitchToManualModeAsync(CancellationToken cancellationToken);
}
