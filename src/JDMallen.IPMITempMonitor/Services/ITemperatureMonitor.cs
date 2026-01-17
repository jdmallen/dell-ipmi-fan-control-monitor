namespace JDMallen.IPMITempMonitor.Services;

/// <summary>
///     Monitors server temperature through IPMI and maintains a rolling average.
/// </summary>
public interface ITemperatureMonitor
{
	/// <summary>
	///     Gets the most recently recorded temperature in Celsius.
	/// </summary>
	int LastRecordedTemperature { get; }

	/// <summary>
	///     Gets the rolling average temperature based on configured number of samples.
	/// </summary>
	double RollingAverageTemperature { get; }

	/// <summary>
	///     Checks the latest temperature from IPMI and updates the rolling average.
	/// </summary>
	/// <param name="cancellationToken">Token to cancel the operation</param>
	Task CheckLatestTemperatureAsync(CancellationToken cancellationToken);
}
