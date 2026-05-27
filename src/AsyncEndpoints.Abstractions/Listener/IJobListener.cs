using AsyncEndpoints.Abstractions.Jobs;

namespace AsyncEndpoints.Abstractions.Listener;

public interface IJobListener
{
    Task<JobRecord?> WaitForNextJobAsync(string channel, IReadOnlySet<int>? partitions, CancellationToken ct = default);
}
