using AsyncEndpoints.Abstractions.Partitioning;
using System.Collections.Concurrent;

namespace AsyncEndpoints.Partitioning;

public sealed class LeaseBasedPartitionAssigner : IPartitionAssigner
{
    private readonly ConcurrentDictionary<int, LeaseInfo> _leases = new();
    private readonly int _partitionCount;
    private int _nextPartition;

    public LeaseBasedPartitionAssigner(int partitionCount = 4)
    {
        _partitionCount = partitionCount;
    }

    public Task<int> AcquirePartitionAsync(string workerId, string channel, CancellationToken ct = default)
    {
        var partition = Interlocked.Increment(ref _nextPartition) % _partitionCount;
        if (partition < 0) partition += _partitionCount;

        _leases[partition] = new LeaseInfo(workerId, DateTime.UtcNow);
        return Task.FromResult(partition);
    }

    public Task RenewLeaseAsync(int partition, string workerId, CancellationToken ct = default)
    {
        if (_leases.TryGetValue(partition, out var existing) && existing.WorkerId == workerId)
        {
            _leases[partition] = new LeaseInfo(workerId, DateTime.UtcNow);
        }
        return Task.CompletedTask;
    }

    public Task ReleasePartitionAsync(int partition, string workerId, CancellationToken ct = default)
    {
        _leases.TryRemove(partition, out _);
        return Task.CompletedTask;
    }

    private sealed record LeaseInfo(string WorkerId, DateTime AcquiredAt);
}
