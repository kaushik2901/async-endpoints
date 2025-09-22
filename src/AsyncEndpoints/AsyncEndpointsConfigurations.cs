using System;

namespace AsyncEndpoints;

public sealed class AsyncEndpointsConfigurations
{
    public int MaximumRetries { get; set; } = 3;
    public AsyncEndpointsWorkerConfigurations WorkerConfigurations { get; set; } = new();
}

public sealed class AsyncEndpointsWorkerConfigurations
{
    public int MaximumConcurrency { get; set; } = Environment.ProcessorCount;
    public int PollingIntervalMs { get; set; } = 1000;
    public int JobTimeoutMinutes { get; set; } = 30;
    public int BatchSize { get; set; } = 5;
    public int MaximumQueueSize { get; set; } = 50;
}
