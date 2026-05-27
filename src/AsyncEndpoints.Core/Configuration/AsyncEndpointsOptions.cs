using System.Text.Json;

namespace AsyncEndpoints.Configuration;

public sealed class AsyncEndpointsOptions
{
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount;
    public int MaxRetries { get; set; } = 3;
    public double RetryDelayBaseSeconds { get; set; } = 2.0;
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan StaleJobTimeout { get; set; } = TimeSpan.FromSeconds(120);
    public TimeSpan PollingMinInterval { get; set; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan PollingMaxInterval { get; set; } = TimeSpan.FromSeconds(30);
    public string DefaultChannel { get; set; } = "default";
    public bool EnablePartitioning { get; set; } = false;
    public bool ObservabilityEnabled { get; set; } = true;
    public bool EnableDistributedJobRecovery { get; set; } = true;
    public int JobTimeoutMinutes { get; set; } = 30;
    public int RecoveryCheckIntervalSeconds { get; set; } = 300;
    public int MaxQueueSize { get; set; } = 50;
    public JsonSerializerOptions? SerializerOptions { get; set; }
}
