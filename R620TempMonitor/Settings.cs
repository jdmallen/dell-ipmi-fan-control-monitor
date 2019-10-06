using System;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.InteropServices;

namespace R620TempMonitor
{
	public class Settings
	{
		public string IpmiHost { get; set; }

		public string IpmiUser { get; set; }

		public string IpmiPassword { get; set; }

		public int MaxTempInC { get; set; }

		public int PollingIntervalInSeconds { get; set; }

		public int BackToManualThresholdInSeconds { get; set; }

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

				throw new PlatformNotSupportedException("Only works on Windows or Linux.");
			}
		}

		
	}

	public enum Platform
	{
		Linux,
		Windows
	}
}
