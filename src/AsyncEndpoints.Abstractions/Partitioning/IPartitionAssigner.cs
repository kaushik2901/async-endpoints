namespace AsyncEndpoints.Abstractions.Partitioning;

public interface IPartitionAssigner
{
	Task<int> AcquirePartitionAsync(string workerId, string channel, CancellationToken ct = default);
	Task RenewLeaseAsync(int partition, string workerId, CancellationToken ct = default);
	Task ReleasePartitionAsync(int partition, string workerId, CancellationToken ct = default);
}
