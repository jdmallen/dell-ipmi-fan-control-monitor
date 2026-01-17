namespace JDMallen.IPMITempMonitor.Services;

/// <summary>
///     Executes IPMI commands through ipmitool with automatic retry handling.
/// </summary>
public interface IIPMICommandExecutor
{
	/// <summary>
	///     Executes an IPMI command with the configured host credentials.
	/// </summary>
	/// <param name="command">The IPMI command to execute (e.g., "raw 0x30 0x30 0x01 0x01")</param>
	/// <param name="cancellationToken">Token to cancel the operation</param>
	/// <returns>The standard output from the ipmitool command</returns>
	Task<string> ExecuteCommandAsync(string command, CancellationToken cancellationToken);
}
