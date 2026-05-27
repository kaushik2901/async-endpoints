using AsyncEndpoints.Abstractions.Partitioning;
using System.Collections.Concurrent;

namespace AsyncEndpoints.Partitioning;

public sealed class PartitionManager
{
    private readonly IPartitionAssigner _assigner;
    private readonly ConcurrentDictionary<int, string> _partitionOwners = new();

    public PartitionManager(IPartitionAssigner assigner)
    {
        _assigner = assigner;
    }

    public async Task<int?> AssignWorkerToPartitionAsync(string workerId, string channel, CancellationToken ct = default)
    {
        var partition = await _assigner.AcquirePartitionAsync(workerId, channel, ct);
        _partitionOwners[partition] = workerId;
        return partition;
    }

    public async Task ReleasePartitionAsync(int partition, string workerId, CancellationToken ct = default)
    {
        await _assigner.ReleasePartitionAsync(partition, workerId, ct);
        _partitionOwners.TryRemove(partition, out _);
    }

    public IReadOnlySet<int> GetAssignedPartitions(string workerId)
    {
        return _partitionOwners
            .Where(kvp => kvp.Value == workerId)
            .Select(kvp => kvp.Key)
            .ToHashSet();
    }
}
