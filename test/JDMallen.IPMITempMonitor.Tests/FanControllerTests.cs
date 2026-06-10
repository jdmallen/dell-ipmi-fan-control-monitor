using FluentAssertions;
using JDMallen.IPMITempMonitor.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace JDMallen.IPMITempMonitor.Tests;

/// <summary>
///     Unit tests for the FanController service.
/// </summary>
public class FanControllerTests
{
	private readonly Mock<ILogger<FanController>> _loggerMock;
	private readonly Mock<IIPMICommandExecutor> _ipmiExecutorMock;
	private readonly Settings _settings;
	private readonly IOptions<Settings> _settingsOptions;

	public FanControllerTests()
	{
		_loggerMock = new Mock<ILogger<FanController>>();
		_ipmiExecutorMock = new Mock<IIPMICommandExecutor>();

		_settings = new Settings
		{
			IPMIHost = "test-host",
			IPMIPassword = "test-password",
			IPMIUser = "test-user",
			RegexToRetrieveTemp = @"(?<=0Eh|0Fh).+(\d{2})",
			BackToManualThresholdInSeconds = 2,
			ManualModeSwitchReattempts = 2,
			ManualModeFanPercentage = 30,
		};

		_settingsOptions = Options.Create(_settings);
	}

	[Fact]
	public async Task SwitchToAutomaticModeAsync_ShouldSetCurrentMode()
	{
		// Arrange
		_ipmiExecutorMock
			.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(string.Empty);

		var controller = new FanController(
			_loggerMock.Object,
			_settingsOptions,
			_ipmiExecutorMock.Object);

		// Act
		await controller.SwitchToAutomaticModeAsync(CancellationToken.None);

		// Assert
		controller.CurrentMode.Should().Be(OperatingMode.AUTOMATIC);
	}

	[Fact]
	public async Task SwitchToAutomaticModeAsync_ShouldExecuteIPMICommand()
	{
		// Arrange
		_ipmiExecutorMock
			.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(string.Empty);

		var controller = new FanController(
			_loggerMock.Object,
			_settingsOptions,
			_ipmiExecutorMock.Object);

		// Act
		await controller.SwitchToAutomaticModeAsync(CancellationToken.None);

		// Assert
		_ipmiExecutorMock.Verify(
			x => x.ExecuteCommandAsync("raw 0x30 0x30 0x01 0x01", It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	public async Task SwitchToManualModeAsync_ShouldExecuteTwoIPMICommands()
	{
		// Arrange
		_ipmiExecutorMock
			.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(string.Empty);

		var controller = new FanController(
			_loggerMock.Object,
			_settingsOptions,
			_ipmiExecutorMock.Object);

		// Record temperature below threshold and wait for threshold
		controller.RecordTemperatureBelowThreshold();
		await Task.Delay(TimeSpan.FromSeconds(2.1));

		// Act
		await controller.SwitchToManualModeAsync(CancellationToken.None);

		// Assert - Should execute disable automatic command and set fan speed command
		_ipmiExecutorMock.Verify(
			x => x.ExecuteCommandAsync("raw 0x30 0x30 0x01 0x00", It.IsAny<CancellationToken>()),
			Times.Once);

		_ipmiExecutorMock.Verify(
			x => x.ExecuteCommandAsync("raw 0x30 0x30 0x02 0xff 0x1E", It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	public async Task SwitchToManualModeAsync_ShouldSetCurrentMode()
	{
		// Arrange
		_ipmiExecutorMock
			.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(string.Empty);

		var controller = new FanController(
			_loggerMock.Object,
			_settingsOptions,
			_ipmiExecutorMock.Object);

		controller.RecordTemperatureBelowThreshold();
		await Task.Delay(TimeSpan.FromSeconds(2.1));

		// Act
		await controller.SwitchToManualModeAsync(CancellationToken.None);

		// Assert
		controller.CurrentMode.Should().Be(OperatingMode.MANUAL);
	}

	[Fact]
	public async Task SwitchToManualModeAsync_ShouldReturnFalse_WhenThresholdNotMet()
	{
		// Arrange
		_ipmiExecutorMock
			.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(string.Empty);

		var controller = new FanController(
			_loggerMock.Object,
			_settingsOptions,
			_ipmiExecutorMock.Object);

		controller.RecordTemperatureBelowThreshold();

		// Act - Try immediately without waiting for threshold
		bool result = await controller.SwitchToManualModeAsync(CancellationToken.None);

		// Assert
		result.Should().BeFalse();
		controller.CurrentMode.Should().Be(OperatingMode.UNKNOWN);
	}

	[Fact]
	public void RecordTemperatureBelowThreshold_ShouldResetRetryCounter()
	{
		// Arrange
		var controller = new FanController(
			_loggerMock.Object,
			_settingsOptions,
			_ipmiExecutorMock.Object);

		// Act
		controller.RecordTemperatureBelowThreshold();

		// Assert
		controller.ShouldAttemptManualModeSwitch().Should().BeTrue();
	}

	[Fact]
	// ReSharper disable once AsyncMethodWithoutAwait
	public async Task ShouldAttemptManualModeSwitch_ShouldReturnTrue_WhenNotInManualMode()
	{
		// Arrange
		var controller = new FanController(
			_loggerMock.Object,
			_settingsOptions,
			_ipmiExecutorMock.Object);

		// Act
		bool shouldAttempt = controller.ShouldAttemptManualModeSwitch();

		// Assert
		shouldAttempt.Should().BeTrue();
	}

	[Fact]
	public async Task ShouldAttemptManualModeSwitch_ShouldReturnFalse_WhenInManualModeAndNoRetries()
	{
		// Arrange
		_ipmiExecutorMock
			.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(string.Empty);

		var controller = new FanController(
			_loggerMock.Object,
			_settingsOptions,
			_ipmiExecutorMock.Object);

		controller.RecordTemperatureBelowThreshold();
		await Task.Delay(TimeSpan.FromSeconds(2.1));

		// Switch to manual and exhaust retries
		await controller.SwitchToManualModeAsync(CancellationToken.None);
		await controller.SwitchToManualModeAsync(CancellationToken.None);

		// Act
		bool shouldAttempt = controller.ShouldAttemptManualModeSwitch();

		// Assert
		shouldAttempt.Should().BeFalse();
	}

	[Fact]
	public void CurrentMode_ShouldStartAsUnknown()
	{
		// Arrange
		var controller = new FanController(
			_loggerMock.Object,
			_settingsOptions,
			_ipmiExecutorMock.Object);

		// Act & Assert
		controller.CurrentMode.Should().Be(OperatingMode.UNKNOWN);
	}

	[Theory]
	[InlineData(30, "0x1E")]
	[InlineData(40, "0x28")]
	[InlineData(50, "0x32")]
	[InlineData(100, "0x64")]
	public async Task SwitchToManualModeAsync_ShouldUseConfiguredFanPercentage(
		int fanPercentage,
		string expectedHex)
	{
		// Arrange
		_settings.ManualModeFanPercentage = fanPercentage;
		_ipmiExecutorMock
			.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(string.Empty);

		var controller = new FanController(
			_loggerMock.Object,
			_settingsOptions,
			_ipmiExecutorMock.Object);

		controller.RecordTemperatureBelowThreshold();
		await Task.Delay(TimeSpan.FromSeconds(2.1));

		// Act
		await controller.SwitchToManualModeAsync(CancellationToken.None);

		// Assert
		_ipmiExecutorMock.Verify(
			x => x.ExecuteCommandAsync(
				$"raw 0x30 0x30 0x02 0xff {expectedHex}",
				It.IsAny<CancellationToken>()),
			Times.Once);
	}
}
