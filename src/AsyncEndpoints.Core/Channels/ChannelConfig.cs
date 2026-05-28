namespace AsyncEndpoints.Core.Channels;

public sealed class ChannelConfig
{
	public string Name { get; set; } = "default";
	public int MaxConcurrency { get; set; } = Environment.ProcessorCount;
	public int MaxRetries { get; set; } = 3;
	public IReadOnlySet<int>? Partitions { get; set; }
}
