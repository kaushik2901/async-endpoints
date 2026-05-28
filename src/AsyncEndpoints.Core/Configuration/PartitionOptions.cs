namespace AsyncEndpoints.Core.Configuration;

public sealed class PartitionOptions
{
	public int PartitionCount { get; set; } = 4;
	public TimeSpan LeaseTimeout { get; set; } = TimeSpan.FromSeconds(60);
	public TimeSpan RebalanceInterval { get; set; } = TimeSpan.FromSeconds(120);
}
