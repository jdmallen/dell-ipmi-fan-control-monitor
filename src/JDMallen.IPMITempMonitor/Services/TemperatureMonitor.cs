using System.Text.RegularExpressions;
using JDMallen.IPMITempMonitor.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Contrib.WaitAndRetry;

namespace JDMallen.IPMITempMonitor.Services;

/// <summary>
///     Monitors server temperature through IPMI and maintains a rolling average.
///     Uses Polly for resilient temperature reading with exponential backoff retry.
/// </summary>
public class TemperatureMonitor(
	ILogger<TemperatureMonitor> logger,
	IOptions<Settings> settings,
	IIPMICommandExecutor ipmiCommandExecutor) : ITemperatureMonitor
{
	private const string CHECK_TEMPERATURE_CONTROL_COMMAND = "sdr type temperature";
	private readonly Settings _settings = settings.Value;
	private readonly List<int> _temperatureHistory = new(settings.Value.RollingAverageNumberOfTemps);

	// Compiled regex with timeout protection to prevent ReDoS attacks
	private readonly Regex _temperatureRegex = new(
		settings.Value.RegexToRetrieveTemp,
		RegexOptions.Compiled | RegexOptions.Multiline,
		TimeSpan.FromSeconds(1));

	/// <inheritdoc />
	public int LastRecordedTemperature { get; private set; }

	/// <inheritdoc />
	public double RollingAverageTemperature =>
		Math.Round(
			_temperatureHistory.Count > 0 ? _temperatureHistory.Average() : 9999,
			1);

	/// <summary>
	///     Calls iDRAC for latest temperature.
	/// </summary>
	/// <remarks>
	///     Ensure that the Regex setting to retrieve the temp(s) has been
	///     updated for your particular system. Mine is set for an R620 system.
	///     The default values provided in this project are meant to parse an
	///     output like the below. The inline comments will reference this
	///     as an example:
	///     Inlet Temp       | 04h | ok  |  7.1 | 20 degrees C
	///     Exhaust Temp     | 01h | ok  |  7.1 | 25 degrees C
	///     Temp             | 0Eh | ok  |  3.1 | 30 degrees C
	///     Temp             | 0Fh | ok  |  3.2 | 31 degrees C
	/// </remarks>
	public async Task CheckLatestTemperatureAsync(CancellationToken cancellationToken)
	{
		// Get the output string like the one in <remarks> above. Using Polly to handle if/when
		// the result is empty, which can happen from time to time.
		int retryCount = _settings.PollyRetryOnFailureCount;

		IEnumerable<TimeSpan> delay = Backoff.ExponentialBackoff(
			TimeSpan.FromMilliseconds(_settings.PollyInitialDelayInMillis),
			retryCount,
			_settings.PollyDelayIncreaseFactor);

		PolicyResult<string> policyExecutionResult = await Policy
			.HandleResult<string>(string.IsNullOrWhiteSpace)
			.WaitAndRetryAsync(
				delay,
				(_, span, iteration, _) =>
				{
					logger.LogEmptyTemperatureResult(
						retryCount - iteration + 1,
						span);
				})
			.ExecuteAndCaptureAsync(
				async token => await ipmiCommandExecutor.ExecuteCommandAsync(
					CHECK_TEMPERATURE_CONTROL_COMMAND,
					token),
				cancellationToken);

		if (policyExecutionResult.Outcome == OutcomeType.Failure)
		{
			logger.LogTemperatureFetchError(
				policyExecutionResult.FinalException,
				retryCount);

			return;
		}

		string result = policyExecutionResult.Result;

		// Using the default of (?<=0Eh|0Fh).+(\d{2}) will return all 2-digit numbers in lines
		// containing "0Eh" or "0Fh"-- in the above example, 30 and 31-- as captured groups.
		MatchCollection matches = _temperatureRegex.Matches(result);

		if (matches.Count == 0)
		{
			return;
		}

		// For each matched line, grab the last capture group (the 2-digit
		// temp) and attempt to convert it to an integer. Find the max
		// int of all the matched lines and return it.
		int maxCpuTemp = matches.Select(x
				=> int.TryParse(x.Groups.Values.LastOrDefault()?.Value, out int temp)
					? temp
					: 0)
			.Max();

		PushTemperature(maxCpuTemp);

		LastRecordedTemperature = maxCpuTemp;
	}

	/// <summary>
	///     Adds a temperature reading to the rolling average history.
	///     Maintains the configured maximum number of samples.
	/// </summary>
	private void PushTemperature(int temp)
	{
		if (_temperatureHistory.Count == _settings.RollingAverageNumberOfTemps)
		{
			_temperatureHistory.RemoveAt(0);
		}

		_temperatureHistory.Add(temp);
	}
}
