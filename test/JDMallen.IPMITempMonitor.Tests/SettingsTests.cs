using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Xunit;

namespace JDMallen.IPMITempMonitor.Tests;

/// <summary>
///     Unit tests for the Settings class.
/// </summary>
public class SettingsTests
{
	/// <summary>
	///     Runs DataAnnotations validation over a Settings instance the same way
	///     ValidateDataAnnotations() does at host startup.
	/// </summary>
	private static List<ValidationResult> Validate(Settings settings)
	{
		var results = new List<ValidationResult>();
		Validator.TryValidateObject(
			settings,
			new ValidationContext(settings),
			results,
			validateAllProperties: true);

		return results;
	}

	private static Settings CreateValidSettings() =>
		new()
		{
			IPMIHost = "192.168.1.100",
			IPMIPassword = "secret",
			IPMIUser = "admin",
			RegexToRetrieveTemp = @"(?<=0Eh|0Fh).+(\d{2})",
		};

	[Fact]
	public void Settings_ShouldHaveDefaultValues()
	{
		// Arrange & Act
		var settings = new Settings
		{
			IPMIHost = "test-host",
			IPMIPassword = "test-password",
			IPMIUser = "test-user",
			RegexToRetrieveTemp = @"(?<=0Eh|0Fh).+(\d{2})",
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
			RollingAverageNumberOfTemps = 15,
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

	[Fact]
	public void Validate_WithValidSettings_ShouldPass()
	{
		// Arrange
		Settings settings = CreateValidSettings();

		// Act
		IReadOnlyList<ValidationResult> results = Validate(settings);

		// Assert
		results.Should().BeEmpty();
	}

	[Fact]
	public void Validate_WithEmptyPassword_ShouldPass()
	{
		// Arrange - empty password is valid (Development uses mocked IPMI calls).
		Settings settings = CreateValidSettings();
		settings.IPMIPassword = string.Empty;

		// Act
		IReadOnlyList<ValidationResult> results = Validate(settings);

		// Assert
		results.Should().BeEmpty();
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	public void Validate_WithMissingHost_ShouldFail(string host)
	{
		// Arrange
		Settings settings = CreateValidSettings();
		settings.IPMIHost = host;

		// Act
		IReadOnlyList<ValidationResult> results = Validate(settings);

		// Assert
		results.Should()
			.Contain(result =>
				result.MemberNames.Contains(nameof(Settings.IPMIHost)));
	}

	[Fact]
	public void Validate_WithInvalidRegex_ShouldFail()
	{
		// Arrange - unbalanced parenthesis is not a valid pattern.
		Settings settings = CreateValidSettings();
		settings.RegexToRetrieveTemp = "(?<=unclosed";

		// Act
		IReadOnlyList<ValidationResult> results = Validate(settings);

		// Assert
		results.Should()
			.Contain(result =>
				result.MemberNames.Contains(nameof(Settings.RegexToRetrieveTemp)));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(201)]
	public void Validate_WithOutOfRangeMaxTemp_ShouldFail(int maxTemp)
	{
		// Arrange
		Settings settings = CreateValidSettings();
		settings.MaxTempInC = maxTemp;

		// Act
		IReadOnlyList<ValidationResult> results = Validate(settings);

		// Assert
		results.Should()
			.Contain(result =>
				result.MemberNames.Contains(nameof(Settings.MaxTempInC)));
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(101)]
	public void Validate_WithOutOfRangeFanPercentage_ShouldFail(int fanPercentage)
	{
		// Arrange
		Settings settings = CreateValidSettings();
		settings.ManualModeFanPercentage = fanPercentage;

		// Act
		IReadOnlyList<ValidationResult> results = Validate(settings);

		// Assert
		results.Should()
			.Contain(result =>
				result.MemberNames.Contains(nameof(Settings.ManualModeFanPercentage)));
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
			PathToIPMIToolIfNotDefault = path,
		};

		// Assert
		settings.PathToIPMIToolIfNotDefault.Should().Be(path);
	}
}
