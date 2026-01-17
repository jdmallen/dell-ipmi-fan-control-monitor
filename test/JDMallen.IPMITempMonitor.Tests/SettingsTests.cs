using FluentAssertions;
using Xunit;

namespace JDMallen.IPMITempMonitor.Tests;

/// <summary>
///     Unit tests for the Settings class.
/// </summary>
public class SettingsTests
{
	[Fact]
	public void Settings_ShouldHaveDefaultValues()
	{
		// Arrange & Act
		var settings = new Settings
		{
			IPMIHost = "test-host",
			IPMIPassword = "test-password",
			IPMIUser = "test-user",
			RegexToRetrieveTemp = @"(?<=0Eh|0Fh).+(\d{2})"
		};

		// Assert
		settings.BackToManualThresholdInSeconds.Should().Be(60);
		settings.ManualModeFanPercentage.Should().Be(30);
		settings.ManualModeSwitchReattempts.Should().Be(2);
		settings.MaxTempInC.Should().Be(50);
		settings.PollingIntervalInSeconds.Should().Be(30);
		settings.PollyDelayIncreaseFactor.Should().Be(2.0);
		settings.PollyInitialDelayInMillis.Should().Be(1000);
		settings.PollyRetryOnFailureCount.Should().Be(5);
		settings.RollingAverageNumberOfTemps.Should().Be(10);
	}

	[Fact]
	public void Settings_ShouldAllowCustomValues()
	{
		// Arrange & Act
		var settings = new Settings
		{
			IPMIHost = "192.168.1.100",
			IPMIPassword = "my-secure-password",
			IPMIUser = "admin",
			RegexToRetrieveTemp = @"(\d{2,3})\s+degrees",
			BackToManualThresholdInSeconds = 120,
			ManualModeFanPercentage = 40,
			ManualModeSwitchReattempts = 3,
			MaxTempInC = 55,
			PathToIPMIToolIfNotDefault = "/custom/path/ipmitool",
			PollingIntervalInSeconds = 60,
			PollyDelayIncreaseFactor = 1.5,
			PollyInitialDelayInMillis = 500,
			PollyRetryOnFailureCount = 3,
			RollingAverageNumberOfTemps = 15
		};

		// Assert
		settings.IPMIHost.Should().Be("192.168.1.100");
		settings.IPMIPassword.Should().Be("my-secure-password");
		settings.IPMIUser.Should().Be("admin");
		settings.RegexToRetrieveTemp.Should().Be(@"(\d{2,3})\s+degrees");
		settings.BackToManualThresholdInSeconds.Should().Be(120);
		settings.ManualModeFanPercentage.Should().Be(40);
		settings.ManualModeSwitchReattempts.Should().Be(3);
		settings.MaxTempInC.Should().Be(55);
		settings.PathToIPMIToolIfNotDefault.Should().Be("/custom/path/ipmitool");
		settings.PollingIntervalInSeconds.Should().Be(60);
		settings.PollyDelayIncreaseFactor.Should().Be(1.5);
		settings.PollyInitialDelayInMillis.Should().Be(500);
		settings.PollyRetryOnFailureCount.Should().Be(3);
		settings.RollingAverageNumberOfTemps.Should().Be(15);
	}

	[Fact]
	public void Platform_ShouldReturnLinuxOrWindows()
	{
		// Act
		Platform platform = Settings.Platform;

		// Assert
		platform.Should().BeOneOf(Platform.Linux, Platform.Windows);
	}

	[Theory]
	[InlineData("")]
	[InlineData(null)]
	public void Settings_ShouldAllowNullOrEmptyPathToIPMITool(string? path)
	{
		// Arrange & Act
		var settings = new Settings
		{
			IPMIHost = "test-host",
			IPMIPassword = "test-password",
			IPMIUser = "test-user",
			RegexToRetrieveTemp = @"(?<=0Eh|0Fh).+(\d{2})",
			PathToIPMIToolIfNotDefault = path
		};

		// Assert
		settings.PathToIPMIToolIfNotDefault.Should().Be(path);
	}
}
