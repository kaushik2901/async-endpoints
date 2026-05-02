using AsyncEndpoints.Abstractions.Jobs;

namespace AsyncEndpoints.Abstractions.Storage;

/// <summary>
/// Defines the contract for job storage and retrieval.
/// </summary>
public interface IJobStore
{
	/// <summary>
	/// Enqueues a new job.
	/// </summary>
	Task EnqueueAsync(JobDescriptor descriptor, CancellationToken ct = default);

	/// <summary>
	/// Dequeues the next available job for the specified channel and partitions.
	/// This operation must be atomic.
	/// </summary>
	Task<JobRecord?> DequeueAsync(string channel, string[]? partitions = null, CancellationToken ct = default);

	/// <summary>
	/// Updates the status of a job.
	/// </summary>
	Task UpdateStatusAsync(
		Guid jobId,
		JobStatus status,
		string? result = null,
		string? error = null,
		int? retryCount = null,
		DateTimeOffset? runAfter = null,
		CancellationToken ct = default);

	/// <summary>
	/// Gets the current record of a job.
	/// </summary>
	Task<JobRecord?> GetJobAsync(Guid jobId, CancellationToken ct = default);

	/// <summary>
	/// Updates the heartbeat for a job to indicate the worker is still alive.
	/// </summary>
	Task HeartbeatAsync(Guid jobId, string workerId, CancellationToken ct = default);

	/// <summary>
	/// Reclaims jobs that haven't been heartbeated within the specified timeout.
	/// </summary>
	Task ReclaimStaleJobsAsync(TimeSpan timeout, CancellationToken ct = default);
}
