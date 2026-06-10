using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using JDMallen.IPMITempMonitor.Validation;
// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace JDMallen.IPMITempMonitor;

public class Settings
{
	[Range(0, int.MaxValue)]
	public int BackToManualThresholdInSeconds { get; set; } = 60;

	[Required(AllowEmptyStrings = false)]
	public required string IPMIHost { get; set; }

	// Not [Required]: an empty password is valid in Development, where IPMI calls are
	// mocked and the password is never used.
	public required string IPMIPassword { get; set; }

	[Required(AllowEmptyStrings = false)]
	public required string IPMIUser { get; set; }

	[Range(0, 100)]
	public int ManualModeFanPercentage { get; set; } = 30;

	[Range(0, int.MaxValue)]
	public int ManualModeSwitchReattempts { get; set; } = 2;

	[Range(1, 200)]
	public int MaxTempInC { get; set; } = 50;

	public string? PathToIPMIToolIfNotDefault { get; set; }

	public static Platform Platform
	{
		get
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				return Platform.Windows;
			}

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				return Platform.Linux;
			}

			throw new PlatformNotSupportedException(
				"Only works on Windows or Linux.");
		}
	}

	[Range(1, int.MaxValue)]
	public int PollingIntervalInSeconds { get; set; } = 30;

	[Range(1.0, double.MaxValue)]
	public double PollyDelayIncreaseFactor { get; set; } = 2.0;

	[Range(1, int.MaxValue)]
	public int PollyInitialDelayInMillis { get; set; } = 1000;

	[Range(0, int.MaxValue)]
	public int PollyRetryOnFailureCount { get; set; } = 5;

	[Required(AllowEmptyStrings = false)]
	[ValidRegex]
	public required string RegexToRetrieveTemp { get; set; }

	[Range(1, int.MaxValue)]
	public int RollingAverageNumberOfTemps { get; set; } = 10;
}

public enum Platform
{
	Linux,
	Windows,
}
