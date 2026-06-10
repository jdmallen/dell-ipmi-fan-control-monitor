using FluentAssertions;
using JDMallen.IPMITempMonitor.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace JDMallen.IPMITempMonitor.Tests;

/// <summary>
///     Unit tests for the Worker background service.
/// </summary>
public class WorkerTests
{
	private readonly Mock<ILogger<Worker>> _loggerMock;
	private readonly Mock<ITemperatureMonitor> _temperatureMonitorMock;
	private readonly Mock<IFanController> _fanControllerMock;
	private readonly Settings _settings;
	private readonly IOptions<Settings> _settingsOptions;
	private readonly IServiceScopeFactory _serviceScopeFactory;

	public WorkerTests()
	{
		_loggerMock = new Mock<ILogger<Worker>>();
		_temperatureMonitorMock = new Mock<ITemperatureMonitor>();
		_fanControllerMock = new Mock<IFanController>();

		_settings = new Settings
		{
			IPMIHost = "test-host",
			IPMIPassword = "test-password",
			IPMIUser = "test-user",
			RegexToRetrieveTemp = @"(?<=0Eh|0Fh).+(\d{2})",
			MaxTempInC = 50,
			PollingIntervalInSeconds = 1,
		};

		_settingsOptions = Options.Create(_settings);

		// Create a service scope factory
		var services = new ServiceCollection();
		ServiceProvider serviceProvider = services.BuildServiceProvider();
		_serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
	}

	[Fact]
	public async Task StartAsync_ShouldCheckTemperature()
	{
		// Arrange
		_temperatureMonitorMock
			.Setup(x => x.LastRecordedTemperature)
			.Returns(45);

		_temperatureMonitorMock
			.Setup(x => x.RollingAverageTemperature)
			.Returns(44.5);

		_fanControllerMock
			.Setup(x => x.CurrentMode)
			.Returns(OperatingMode.AUTOMATIC);

		var worker = new Worker(
			_loggerMock.Object,
			_settingsOptions,
			_serviceScopeFactory,
			_temperatureMonitorMock.Object,
			_fanControllerMock.Object);

		// Act
		await worker.StartAsync(CancellationToken.None);

		// Assert
		_temperatureMonitorMock.Verify(
			x => x.CheckLatestTemperatureAsync(It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	public async Task StartAsync_ShouldSwitchToAutomaticMode()
	{
		// Arrange
		_temperatureMonitorMock
			.Setup(x => x.LastRecordedTemperature)
			.Returns(45);

		_temperatureMonitorMock
			.Setup(x => x.RollingAverageTemperature)
			.Returns(44.5);

		var worker = new Worker(
			_loggerMock.Object,
			_settingsOptions,
			_serviceScopeFactory,
			_temperatureMonitorMock.Object,
			_fanControllerMock.Object);

		// Act
		await worker.StartAsync(CancellationToken.None);

		// Assert
		_fanControllerMock.Verify(
			x => x.SwitchToAutomaticModeAsync(It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	public void StopAsync_ShouldNotThrow()
	{
		// Arrange
		_temperatureMonitorMock
			.Setup(x => x.LastRecordedTemperature)
			.Returns(45);

		_temperatureMonitorMock
			.Setup(x => x.RollingAverageTemperature)
			.Returns(44.5);

		var worker = new Worker(
			_loggerMock.Object,
			_settingsOptions,
			_serviceScopeFactory,
			_temperatureMonitorMock.Object,
			_fanControllerMock.Object);

		// Act
		Func<Task> act = async () => await worker.StopAsync(CancellationToken.None);

		// Assert
		act.Should().NotThrowAsync();
	}

	[Fact]
	public void Constructor_ShouldAcceptAllDependencies()
	{
		// Act
		var worker = new Worker(
			_loggerMock.Object,
			_settingsOptions,
			_serviceScopeFactory,
			_temperatureMonitorMock.Object,
			_fanControllerMock.Object);

		// Assert
		worker.Should().NotBeNull();
	}

	[Theory]
	[InlineData(30)]
	[InlineData(60)]
	[InlineData(120)]
	public void Constructor_WithVariousPollingIntervals_ShouldNotThrow(int pollingInterval)
	{
		// Arrange
		_settings.PollingIntervalInSeconds = pollingInterval;

		// Act
		var worker = new Worker(
			_loggerMock.Object,
			_settingsOptions,
			_serviceScopeFactory,
			_temperatureMonitorMock.Object,
			_fanControllerMock.Object);

		// Assert
		worker.Should().NotBeNull();
	}
}
