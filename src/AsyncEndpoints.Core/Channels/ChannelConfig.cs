namespace AsyncEndpoints.Channels;

public sealed record ChannelConfig
{
    public string Name { get; init; } = "default";
    public int MaxConcurrency { get; init; } = Environment.ProcessorCount;
    public int MaxRetries { get; init; } = 3;
    public IReadOnlySet<int>? Partitions { get; init; }
}
