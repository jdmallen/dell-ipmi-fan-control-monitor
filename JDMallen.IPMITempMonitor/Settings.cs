using System;
using System.Runtime.InteropServices;

namespace JDMallen.IPMITempMonitor
{
	public class Settings
	{
		public string IpmiHost { get; set; }

		public string IpmiUser { get; set; }

		public string IpmiPassword { get; set; }

		public string PathToIpmiToolIfNotDefault { get; set; }

		public string RegexToRetrieveTemp { get; set; }

		public int MaxTempInC { get; set; } = 50;

		public int ManualModeFanPercentage { get; set; } = 30;

		public int PollingIntervalInSeconds { get; set; } = 30;

		public int RollingAverageNumberOfTemps { get; set; } = 10;

		public int BackToManualThresholdInSeconds { get; set; } = 60;

		public Platform Platform
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
	}

	public enum Platform
	{
		Linux,
		Windows
	}
}
