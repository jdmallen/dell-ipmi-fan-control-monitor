using System.Diagnostics.CodeAnalysis;

namespace JDMallen.IPMITempMonitor;

/// <summary>
///     Represents the fan control operating mode.
/// </summary>
[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum OperatingMode
{
	/// <summary>
	///     Operating mode is not yet determined.
	/// </summary>
	UNKNOWN,

	/// <summary>
	///     Automatic mode - BIOS controls fan speed based on temperature.
	/// </summary>
	AUTOMATIC,

	/// <summary>
	///     Manual mode - Fan speed is set to a fixed percentage.
	/// </summary>
	MANUAL,
}
