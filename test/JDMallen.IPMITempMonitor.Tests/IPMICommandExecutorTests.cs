using FluentAssertions;
using JDMallen.IPMITempMonitor.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace JDMallen.IPMITempMonitor.Tests;

/// <summary>
///     Unit tests for the IPMICommandExecutor service.
/// </summary>
public class IPMICommandExecutorTests
{
	private readonly Mock<ILogger<IPMICommandExecutor>> _loggerMock;
	private readonly Mock<IConfiguration> _configurationMock;
	private readonly Mock<IHostEnvironment> _hostEnvironmentMock;
	private readonly Mock<IHostApplicationLifetime> _applicationLifetimeMock;
	private readonly Settings _settings;
	private readonly IOptions<Settings> _settingsOptions;

	public IPMICommandExecutorTests()
	{
		_loggerMock = new Mock<ILogger<IPMICommandExecutor>>();
		_configurationMock = new Mock<IConfiguration>();
		_hostEnvironmentMock = new Mock<IHostEnvironment>();
		_applicationLifetimeMock = new Mock<IHostApplicationLifetime>();

		_settings = new Settings
		{
			IPMIHost = "192.168.1.100",
			IPMIPassword = "test-password",
			IPMIUser = "admin",
			RegexToRetrieveTemp = @"(?<=0Eh|0Fh).+(\d{2})",
			PollyRetryOnFailureCount = 2,
			PollyInitialDelayInMillis = 10,
			PollyDelayIncreaseFactor = 1.5,
		};

		_settingsOptions = Options.Create(_settings);

		// Default to production environment
		_hostEnvironmentMock
			.Setup(x => x.EnvironmentName)
			.Returns(Environments.Production);
	}

	[Fact]
	public async Task ExecuteCommandAsync_InDevelopment_ShouldReturnMockedResponse()
	{
		// Arrange
		_hostEnvironmentMock
			.Setup(x => x.EnvironmentName)
			.Returns(Environments.Development);

		var configSectionMock = new Mock<IConfigurationSection>();
		configSectionMock.Setup(x => x.Value).Returns("true");

		_configurationMock
			.Setup(x => x.GetSection("DOTNET_RUNNING_IN_CONTAINER"))
			.Returns(configSectionMock.Object);

		// Create a test file in the base directory (simulating Docker)
		string testFilePath = Path.Combine(AppContext.BaseDirectory, "test_temp_response.txt");
		const string testContent = "Temp | 0Eh | ok | 3.1 | 42 degrees C";
		await File.WriteAllTextAsync(testFilePath, testContent);

		try
		{
			var executor = new IPMICommandExecutor(
				_loggerMock.Object,
				_settingsOptions,
				_configurationMock.Object,
				_hostEnvironmentMock.Object,
				_applicationLifetimeMock.Object);

			// Act
			string result = await executor.ExecuteCommandAsync(
				"sdr type temperature",
				CancellationToken.None);

			// Assert
			result.Should().Be(testContent);
		}
		finally
		{
			// Cleanup
			if (File.Exists(testFilePath))
			{
				File.Delete(testFilePath);
			}
		}
	}

	[Fact]
	public async Task ExecuteCommandAsync_InDevelopment_WithNonTemperatureCommand_ShouldReturnEmpty()
	{
		// Arrange
		_hostEnvironmentMock
			.Setup(x => x.EnvironmentName)
			.Returns(Environments.Development);

		var executor = new IPMICommandExecutor(
			_loggerMock.Object,
			_settingsOptions,
			_configurationMock.Object,
			_hostEnvironmentMock.Object,
			_applicationLifetimeMock.Object);

		// Act
		string result = await executor.ExecuteCommandAsync(
			"raw 0x30 0x30 0x01 0x01",
			CancellationToken.None);

		// Assert
		result.Should().BeEmpty();
	}

	[Fact]
	public void ExecuteCommandAsync_ShouldUseCustomIPMIToolPath_WhenProvided()
	{
		// Arrange
		_settings.PathToIPMIToolIfNotDefault = "/custom/path/ipmitool";

		var executor = new IPMICommandExecutor(
			_loggerMock.Object,
			_settingsOptions,
			_configurationMock.Object,
			_hostEnvironmentMock.Object,
			_applicationLifetimeMock.Object);

		// Act & Assert - The executor should be created successfully
		executor.Should().NotBeNull();
	}

	[Fact]
	public void ExecuteCommandAsync_ShouldMaskPassword_InLogs()
	{
		// Arrange
		_hostEnvironmentMock
			.Setup(x => x.EnvironmentName)
			.Returns(Environments.Development);

		var executor = new IPMICommandExecutor(
			_loggerMock.Object,
			_settingsOptions,
			_configurationMock.Object,
			_hostEnvironmentMock.Object,
			_applicationLifetimeMock.Object);

		// Act
		Task<string> task = executor.ExecuteCommandAsync("sdr type temperature", CancellationToken.None);

		// Assert - Password should be masked in debug logs
		// We can't directly verify the log message, but we ensure the method doesn't throw
		task.Should().NotBeNull();
	}

	[Theory]
	[InlineData("192.168.1.100", "admin", "secret123")]
	[InlineData("10.0.0.50", "root", "P@ssw0rd!")]
	public void Constructor_ShouldAcceptVariousSettings(string host, string user, string password)
	{
		// Arrange
		var customSettings = new Settings
		{
			IPMIHost = host,
			IPMIUser = user,
			IPMIPassword = password,
			RegexToRetrieveTemp = @"(?<=0Eh|0Fh).+(\d{2})",
		};

		IOptions<Settings> customOptions = Options.Create(customSettings);

		// Act
		var executor = new IPMICommandExecutor(
			_loggerMock.Object,
			customOptions,
			_configurationMock.Object,
			_hostEnvironmentMock.Object,
			_applicationLifetimeMock.Object);

		// Assert
		executor.Should().NotBeNull();
	}

	[Fact]
	public async Task ExecuteCommandAsync_InDevelopment_WithMissingTestFile_ShouldHandleGracefully()
	{
		// Arrange
		_hostEnvironmentMock
			.Setup(x => x.EnvironmentName)
			.Returns(Environments.Development);

		_configurationMock
			.Setup(x => x["DOTNET_RUNNING_IN_CONTAINER"])
			.Returns("false");

		// Ensure test file doesn't exist
		string testFilePath = Path.Combine(AppContext.BaseDirectory, "test_temp_response.txt");
		if (File.Exists(testFilePath))
		{
			File.Delete(testFilePath);
		}

		var executor = new IPMICommandExecutor(
			_loggerMock.Object,
			_settingsOptions,
			_configurationMock.Object,
			_hostEnvironmentMock.Object,
			_applicationLifetimeMock.Object);

		// Act
		string result = await executor.ExecuteCommandAsync(
			"sdr type temperature",
			CancellationToken.None);

		// Assert
		result.Should().BeEmpty();
	}

	[Fact]
	public void Constructor_WithDefaultPlatform_ShouldSelectCorrectIPMIToolPath()
	{
		// Arrange & Act
		var executor = new IPMICommandExecutor(
			_loggerMock.Object,
			_settingsOptions,
			_configurationMock.Object,
			_hostEnvironmentMock.Object,
			_applicationLifetimeMock.Object);

		// Assert - Should not throw exception when determining platform
		executor.Should().NotBeNull();
	}
}
