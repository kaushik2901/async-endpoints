namespace AsyncEndpoints.Configuration;

/// <summary>
/// Configuration settings for the AsyncEndpoints library.
/// </summary>
public sealed class AsyncEndpointsConfigurations
{
	/// <summary>
	/// Gets or sets the worker-specific configurations.
	/// </summary>
	public AsyncEndpointsWorkerConfigurations WorkerConfigurations { get; set; } = new();

	/// <summary>
	/// Gets or sets the job-manager-specific configurations.
	/// </summary>
	public AsyncEndpointsJobManagerConfiguration JobManagerConfiguration { get; set; } = new();

	/// <summary>
	/// Gets or sets the response-specific configurations.
	/// </summary>
	public AsyncEndpointsResponseConfigurations ResponseConfigurations { get; set; } = new();

	/// <summary>
	/// Gets or sets the observability-specific configurations.
	/// </summary>
	public AsyncEndpointsObservabilityConfigurations ObservabilityConfigurations { get; set; } = new();
}
