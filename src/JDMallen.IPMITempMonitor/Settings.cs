using System.Runtime.InteropServices;

namespace JDMallen.IPMITempMonitor;

public class Settings
{
	public int BackToManualThresholdInSeconds { get; set; } = 60;

	public required string IPMIHost { get; set; }

	public required string IPMIPassword { get; set; }

	public required string IPMIUser { get; set; }

	public int ManualModeFanPercentage { get; set; } = 30;

	public int ManualModeSwitchReattempts { get; set; } = 2;

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

	public int PollingIntervalInSeconds { get; set; } = 30;

	public double PollyDelayIncreaseFactor { get; set; } = 2.0;

	public int PollyInitialDelayInMillis { get; set; } = 1000;

	public int PollyRetryOnFailureCount { get; set; } = 5;

	public required string RegexToRetrieveTemp { get; set; }

	public int RollingAverageNumberOfTemps { get; set; } = 10;
}

public enum Platform
{
	Linux,
	Windows,
}
