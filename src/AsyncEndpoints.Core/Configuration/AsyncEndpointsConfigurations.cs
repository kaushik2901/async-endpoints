namespace AsyncEndpoints.Configuration;

public sealed class AsyncEndpointsConfigurations
{
	public AsyncEndpointsWorkerConfigurations WorkerConfigurations { get; set; } = new();
	public AsyncEndpointsJobManagerConfigurations JobManagerConfigurations { get; set; } = new();
	public AsyncEndpointsObservabilityConfigurations ObservabilityConfigurations { get; set; } = new();
}
