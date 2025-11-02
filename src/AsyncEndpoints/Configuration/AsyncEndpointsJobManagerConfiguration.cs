namespace AsyncEndpoints.Configuration;

/// <summary>
/// Configuration settings for the AsyncEndpoints job manager.
/// </summary>
public sealed class AsyncEndpointsJobManagerConfiguration
{
	/// <summary>
	/// Gets or sets the default maximum number of retries for failed jobs.
	/// </summary>
	public int DefaultMaxRetries { get; set; } = AsyncEndpointsConstants.MaximumRetries;

	/// <summary>
	/// Gets or sets the base delay in seconds for job retry exponential backoff.
	/// </summary>
	public double RetryDelayBaseSeconds { get; set; } = 2.0;
}
