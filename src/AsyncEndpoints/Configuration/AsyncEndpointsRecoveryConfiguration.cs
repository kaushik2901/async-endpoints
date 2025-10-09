namespace AsyncEndpoints.Configuration;

/// <summary>
/// Configuration options for AsyncEndpoints job recovery functionality
/// </summary>
public class AsyncEndpointsRecoveryConfiguration
{
	/// <summary>
	/// Whether to enable the distributed job recovery service
	/// </summary>
	public bool EnableDistributedJobRecovery { get; set; } = true;

	/// <summary>
	/// Time in minutes after which a job in progress is considered stuck
	/// </summary>
	public int JobTimeoutMinutes { get; set; } = 30;

	/// <summary>
	/// Interval in seconds between recovery checks
	/// </summary>
	public int RecoveryCheckIntervalSeconds { get; set; } = 300; // 5 minutes

	/// <summary>
	/// Maximum number of times to retry a failed job
	/// </summary>
	public int MaximumRetries { get; set; } = 3;
}
