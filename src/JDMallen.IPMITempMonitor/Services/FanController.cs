using JDMallen.IPMITempMonitor.Logging;
using Microsoft.Extensions.Options;

namespace JDMallen.IPMITempMonitor.Services;

/// <summary>
///     Controls server fan operation mode between automatic and manual.
///     Implements safety thresholds and retry logic for mode switching.
/// </summary>
public class FanController(
	ILogger<FanController> logger,
	IOptions<Settings> settings,
	IIPMICommandExecutor ipmiCommandExecutor) : IFanController
{
	private const string ENABLE_AUTOMATIC_TEMP_CONTROL_COMMAND = "raw 0x30 0x30 0x01 0x01";
	private const string DISABLE_AUTOMATIC_TEMP_CONTROL_COMMAND = "raw 0x30 0x30 0x01 0x00";
	private const string STATIC_FAN_SPEED_FORMAT_STRING = "raw 0x30 0x30 0x02 0xff 0x{0}";
	private readonly Settings _settings = settings.Value;
	private int _manualSwitchAttemptCount;
	private DateTime _timeFellBelowTemp = DateTime.MinValue;

	/// <inheritdoc />
	public OperatingMode CurrentMode { get; private set; } = OperatingMode.UNKNOWN;

	/// <inheritdoc />
	public async Task SwitchToAutomaticModeAsync(CancellationToken cancellationToken)
	{
		logger.LogSwitchingToAutomatic(OperatingMode.AUTOMATIC.ToString("G"));

		await ipmiCommandExecutor.ExecuteCommandAsync(
			ENABLE_AUTOMATIC_TEMP_CONTROL_COMMAND,
			cancellationToken);

		CurrentMode = OperatingMode.AUTOMATIC;
	}

	/// <inheritdoc />
	public async Task<bool> SwitchToManualModeAsync(CancellationToken cancellationToken)
	{
		TimeSpan timeSinceLastActivation = DateTime.UtcNow - _timeFellBelowTemp;
		TimeSpan threshold = TimeSpan.FromSeconds(_settings.BackToManualThresholdInSeconds);

		// Safety check: ensure enough time has passed since temperature dropped
		if (timeSinceLastActivation < threshold)
		{
			var secondsRemaining =
				(int)(threshold - timeSinceLastActivation).TotalSeconds;

			logger.LogManualDelayThresholdNotMet(
				OperatingMode.MANUAL.ToString("G"),
				OperatingMode.AUTOMATIC.ToString("G"),
				secondsRemaining,
				secondsRemaining == 1 ? "second" : "seconds");

			return false;
		}

		logger.LogSwitchingToManual(
			OperatingMode.MANUAL.ToString("G"),
			_settings.ManualModeSwitchReattempts - _manualSwitchAttemptCount + 1,
			_settings.ManualModeSwitchReattempts);

		// Disable automatic temperature control
		await ipmiCommandExecutor.ExecuteCommandAsync(
			DISABLE_AUTOMATIC_TEMP_CONTROL_COMMAND,
			cancellationToken);

		// Set static fan speed
		string fanSpeedCommand = string.Format(
			STATIC_FAN_SPEED_FORMAT_STRING,
			_settings.ManualModeFanPercentage.ToString("X"));

		await ipmiCommandExecutor.ExecuteCommandAsync(fanSpeedCommand, cancellationToken);

		CurrentMode = OperatingMode.MANUAL;

		if (_manualSwitchAttemptCount >= 1)
		{
			_manualSwitchAttemptCount--;
		}

		return true;
	}

	/// <inheritdoc />
	public void RecordTemperatureBelowThreshold()
	{
		// Record the first record of when the temp dipped below the max temp threshold.
		// This is an extra safety measure to ensure that AUTOMATIC mode isn't turned off
		// too soon.
		_timeFellBelowTemp = DateTime.UtcNow;

		// Reset the number of times we attempt to switch to MANUAL, based on setting
		// (default: 2). Note that if the temperature goes above threshold between attempts,
		// subsequent attempts are skipped and it switches to AUTOMATIC fan control mode
		// (as one would hope).
		_manualSwitchAttemptCount = _settings.ManualModeSwitchReattempts;
	}

	/// <inheritdoc />
	public bool ShouldAttemptManualModeSwitch()
		=> CurrentMode != OperatingMode.MANUAL || _manualSwitchAttemptCount > 0;
}
