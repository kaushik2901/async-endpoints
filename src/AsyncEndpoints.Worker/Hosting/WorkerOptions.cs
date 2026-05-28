namespace AsyncEndpoints.Worker.Hosting;

public sealed class WorkerOptions
{
	public Guid WorkerId { get; set; } = Guid.NewGuid();
	public int MaxConcurrency { get; set; } = Environment.ProcessorCount;
	public int MaxRetries { get; set; } = 3;
	public double RetryDelayBaseSeconds { get; set; } = 2.0;
	public TimeSpan PollingIntervalMin { get; set; } = TimeSpan.FromMilliseconds(100);
	public TimeSpan PollingIntervalMax { get; set; } = TimeSpan.FromSeconds(30);
	public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(30);
	public TimeSpan StaleJobTimeout { get; set; } = TimeSpan.FromSeconds(120);
	public int MaxQueueSize { get; set; } = 50;
	public string DefaultChannel { get; set; } = "default";
}
