namespace AsyncEndpoints.Configuration;

/// <summary>
/// Configuration settings for AsyncEndpoints observability features.
/// </summary>
public sealed class AsyncEndpointsObservabilityConfigurations
{
	/// <summary>
	/// Gets or sets a value indicating whether metrics collection is enabled.
	/// </summary>
	public bool EnableMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether distributed tracing is enabled.
	/// </summary>
	public bool EnableTracing { get; set; } = true;
}
