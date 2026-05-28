using AsyncEndpoints.Abstractions.Jobs;

namespace AsyncEndpoints.Abstractions.Storage;

public interface IJobStore
{
	Task<Guid> EnqueueAsync(JobDescriptor descriptor, CancellationToken ct = default);
	Task<JobRecord?> DequeueAsync(string channel, IReadOnlySet<int>? partitions, CancellationToken ct = default);
	Task UpdateStatusAsync(Guid jobId, JobStatus status, string? result = null, CancellationToken ct = default);
	Task<JobRecord?> GetStatusAsync(Guid jobId, CancellationToken ct = default);
	Task HeartbeatAsync(Guid jobId, CancellationToken ct = default);
	Task<int> ReclaimStaleJobsAsync(TimeSpan staleTimeout, CancellationToken ct = default);
}
