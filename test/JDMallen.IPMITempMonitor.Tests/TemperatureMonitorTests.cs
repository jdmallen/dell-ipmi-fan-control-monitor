using FluentAssertions;
using JDMallen.IPMITempMonitor.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace JDMallen.IPMITempMonitor.Tests;

/// <summary>
///     Unit tests for the TemperatureMonitor service.
/// </summary>
public class TemperatureMonitorTests
{
	private readonly Mock<ILogger<TemperatureMonitor>> _loggerMock;
	private readonly Mock<IIPMICommandExecutor> _ipmiExecutorMock;
	private readonly Settings _settings;
	private readonly IOptions<Settings> _settingsOptions;

	public TemperatureMonitorTests()
	{
		_loggerMock = new Mock<ILogger<TemperatureMonitor>>();
		_ipmiExecutorMock = new Mock<IIPMICommandExecutor>();

		_settings = new Settings
		{
			IPMIHost = "test-host",
			IPMIPassword = "test-password",
			IPMIUser = "test-user",
			RegexToRetrieveTemp = @"(?<=0Eh|0Fh).+(\d{2})",
			RollingAverageNumberOfTemps = 3,
			PollyRetryOnFailureCount = 2,
			PollyInitialDelayInMillis = 10,
			PollyDelayIncreaseFactor = 1.5
		};

		_settingsOptions = Options.Create(_settings);
	}

	[Fact]
	public async Task CheckLatestTemperatureAsync_ShouldUpdateLastRecordedTemperature()
	{
		// Arrange
		const string ipmiResponse = """
		                            Inlet Temp       | 04h | ok  |  7.1 | 20 degrees C
		                            Exhaust Temp     | 01h | ok  |  7.1 | 25 degrees C
		                            Temp             | 0Eh | ok  |  3.1 | 45 degrees C
		                            Temp             | 0Fh | ok  |  3.2 | 48 degrees C
		                            """;

		_ipmiExecutorMock
			.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(ipmiResponse);

		var monitor = new TemperatureMonitor(_loggerMock.Object, _settingsOptions, _ipmiExecutorMock.Object);

		// Act
		await monitor.CheckLatestTemperatureAsync(CancellationToken.None);

		// Assert
		monitor.LastRecordedTemperature.Should().Be(48);
	}

	[Fact]
	public async Task CheckLatestTemperatureAsync_ShouldCalculateRollingAverage()
	{
		// Arrange
		const string response1 = "Temp | 0Eh | ok | 3.1 | 30 degrees C";
		const string response2 = "Temp | 0Eh | ok | 3.1 | 40 degrees C";
		const string response3 = "Temp | 0Eh | ok | 3.1 | 50 degrees C";

		var responses = new Queue<string>(new[] { response1, response2, response3 });
		_ipmiExecutorMock
			.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(() => responses.Dequeue());

		var monitor = new TemperatureMonitor(_loggerMock.Object, _settingsOptions, _ipmiExecutorMock.Object);

		// Act
		await monitor.CheckLatestTemperatureAsync(CancellationToken.None);
		await monitor.CheckLatestTemperatureAsync(CancellationToken.None);
		await monitor.CheckLatestTemperatureAsync(CancellationToken.None);

		// Assert
		monitor.RollingAverageTemperature.Should().Be(40.0);
	}

	[Fact]
	public async Task CheckLatestTemperatureAsync_ShouldMaintainMaxHistorySize()
	{
		// Arrange
		const string response1 = "Temp | 0Eh | ok | 3.1 | 30 degrees C";
		const string response2 = "Temp | 0Eh | ok | 3.1 | 40 degrees C";
		const string response3 = "Temp | 0Eh | ok | 3.1 | 50 degrees C";
		const string response4 = "Temp | 0Eh | ok | 3.1 | 60 degrees C";

		var responses = new Queue<string>(new[] { response1, response2, response3, response4 });
		_ipmiExecutorMock
			.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(() => responses.Dequeue());

		var monitor = new TemperatureMonitor(_loggerMock.Object, _settingsOptions, _ipmiExecutorMock.Object);

		// Act - Add 4 readings when max is 3
		await monitor.CheckLatestTemperatureAsync(CancellationToken.None);
		await monitor.CheckLatestTemperatureAsync(CancellationToken.None);
		await monitor.CheckLatestTemperatureAsync(CancellationToken.None);
		await monitor.CheckLatestTemperatureAsync(CancellationToken.None);

		// Assert - Average should be (40 + 50 + 60) / 3 = 50.0 (first value should be removed)
		monitor.RollingAverageTemperature.Should().Be(50.0);
	}

	[Fact]
	public async Task CheckLatestTemperatureAsync_ShouldHandleEmptyResult()
	{
		// Arrange
		_ipmiExecutorMock
			.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(string.Empty);

		var monitor = new TemperatureMonitor(_loggerMock.Object, _settingsOptions, _ipmiExecutorMock.Object);

		// Act
		await monitor.CheckLatestTemperatureAsync(CancellationToken.None);

		// Assert
		monitor.LastRecordedTemperature.Should().Be(0);
		monitor.RollingAverageTemperature.Should().Be(9999);
	}

	[Fact]
	public async Task CheckLatestTemperatureAsync_ShouldHandleNoMatches()
	{
		// Arrange
		const string ipmiResponse = "Some invalid response with no temperature data";

		_ipmiExecutorMock
			.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(ipmiResponse);

		var monitor = new TemperatureMonitor(_loggerMock.Object, _settingsOptions, _ipmiExecutorMock.Object);

		// Act
		await monitor.CheckLatestTemperatureAsync(CancellationToken.None);

		// Assert
		monitor.LastRecordedTemperature.Should().Be(0);
	}

	[Fact]
	public async Task CheckLatestTemperatureAsync_ShouldSelectMaxTemperature()
	{
		// Arrange
		const string ipmiResponse = """
		                            Temp             | 0Eh | ok  |  3.1 | 35 degrees C
		                            Temp             | 0Fh | ok  |  3.2 | 72 degrees C
		                            Temp             | 0Eh | ok  |  3.1 | 45 degrees C
		                            """;

		_ipmiExecutorMock
			.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(ipmiResponse);

		var monitor = new TemperatureMonitor(_loggerMock.Object, _settingsOptions, _ipmiExecutorMock.Object);

		// Act
		await monitor.CheckLatestTemperatureAsync(CancellationToken.None);

		// Assert
		monitor.LastRecordedTemperature.Should().Be(72);
	}

	[Fact]
	public void RollingAverageTemperature_ShouldReturn9999_WhenNoHistory()
	{
		// Arrange
		var monitor = new TemperatureMonitor(_loggerMock.Object, _settingsOptions, _ipmiExecutorMock.Object);

		// Act
		double average = monitor.RollingAverageTemperature;

		// Assert
		average.Should().Be(9999);
	}

	[Theory]
	[InlineData("Temp | 0Eh | ok | 3.1 | 25 degrees C", 25)]
	[InlineData("Temp | 0Fh | ok | 3.2 | 99 degrees C", 99)]
	[InlineData("Temp | 0Eh | ok | 3.1 | 10 degrees C", 10)]
	public async Task CheckLatestTemperatureAsync_ShouldParseVariousTemperatures(
		string ipmiResponse,
		int expectedTemp)
	{
		// Arrange
		_ipmiExecutorMock
			.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(ipmiResponse);

		var monitor = new TemperatureMonitor(_loggerMock.Object, _settingsOptions, _ipmiExecutorMock.Object);

		// Act
		await monitor.CheckLatestTemperatureAsync(CancellationToken.None);

		// Assert
		monitor.LastRecordedTemperature.Should().Be(expectedTemp);
	}
}
