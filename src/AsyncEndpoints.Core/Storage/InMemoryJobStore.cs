using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Storage;
using System.Collections.Concurrent;

namespace AsyncEndpoints.Core.Storage;

public class InMemoryJobStore : IJobStore
{
	private readonly TimeProvider _dateTimeProvider;
	private readonly ConcurrentDictionary<Guid, JobRecord> _jobs = new();

	public InMemoryJobStore(TimeProvider dateTimeProvider)
	{
		_dateTimeProvider = dateTimeProvider;
	}

	public Task<Guid> EnqueueAsync(JobDescriptor descriptor, CancellationToken ct = default)
	{
		var now = _dateTimeProvider.GetUtcNow().UtcDateTime;
		var jobId = Guid.NewGuid();
		var partition = descriptor.PartitionKey is not null
			? Math.Abs(descriptor.PartitionKey.GetHashCode(StringComparison.Ordinal)) % 100
			: (int?)null;

		var record = new JobRecord
		{
			JobId = jobId,
			JobName = descriptor.JobName,
			Channel = descriptor.Channel ?? "default",
			Priority = descriptor.Priority,
			Partition = partition,
			Payload = descriptor.Payload,
			Status = JobStatus.Queued,
			RetryCount = 0,
			MaxRetries = 3,
			CreatedAt = now,
			Metadata = descriptor.Metadata
		};

		if (!_jobs.TryAdd(jobId, record))
			throw new InvalidOperationException($"Failed to enqueue job {jobId}");

		return Task.FromResult(jobId);
	}

	public Task<JobRecord?> DequeueAsync(string channel, IReadOnlySet<int>? partitions, CancellationToken ct = default)
	{
		var candidates = _jobs.Values
			.Where(j => string.Equals(j.Channel, channel, StringComparison.OrdinalIgnoreCase))
			.Where(j => j.Status == JobStatus.Queued)
			.Where(j => partitions is null || partitions.Count == 0 || (j.Partition.HasValue && partitions.Contains(j.Partition.Value)))
			.OrderByDescending(j => j.Priority)
			.ThenBy(j => j.CreatedAt)
			.ToList();

		foreach (var record in candidates)
		{
			var updated = record with
			{
				Status = JobStatus.Processing,
				StartedAt = _dateTimeProvider.GetUtcNow().UtcDateTime,
				LastHeartbeat = _dateTimeProvider.GetUtcNow().UtcDateTime
			};

			if (_jobs.TryUpdate(record.JobId, updated, record))
				return Task.FromResult<JobRecord?>(updated);
		}

		return Task.FromResult<JobRecord?>(null);
	}

	public Task UpdateStatusAsync(Guid jobId, JobStatus status, string? result = null, CancellationToken ct = default)
	{
		while (true)
		{
			if (!_jobs.TryGetValue(jobId, out var record))
				throw new KeyNotFoundException($"Job {jobId} not found");

			if (!IsValidTransition(record.Status, status))
				throw new InvalidOperationException($"Invalid state transition from {record.Status} to {status}");

			var updated = record with
			{
				Status = status,
				Result = result ?? record.Result,
				StartedAt = status is JobStatus.Queued ? null : record.StartedAt,
				WorkerId = status is JobStatus.Queued ? null : record.WorkerId,
				CompletedAt = status is JobStatus.Completed or JobStatus.Failed or JobStatus.DeadLettered
					? _dateTimeProvider.GetUtcNow().UtcDateTime
					: record.CompletedAt
			};

			if (_jobs.TryUpdate(jobId, updated, record))
				return Task.CompletedTask;
		}
	}

	public Task<JobRecord?> GetStatusAsync(Guid jobId, CancellationToken ct = default)
	{
		_jobs.TryGetValue(jobId, out var record);
		return Task.FromResult(record);
	}

	public Task HeartbeatAsync(Guid jobId, CancellationToken ct = default)
	{
		while (true)
		{
			if (!_jobs.TryGetValue(jobId, out var record))
				throw new KeyNotFoundException($"Job {jobId} not found");

			var updated = record with { LastHeartbeat = _dateTimeProvider.GetUtcNow().UtcDateTime };
			if (_jobs.TryUpdate(jobId, updated, record))
				return Task.CompletedTask;
		}
	}

	public Task<int> ReclaimStaleJobsAsync(TimeSpan staleTimeout, CancellationToken ct = default)
	{
		var cutoff = _dateTimeProvider.GetUtcNow().UtcDateTime - staleTimeout;
		var reclaimed = 0;

		foreach (var kvp in _jobs)
		{
			var record = kvp.Value;
			if (record.Status == JobStatus.Processing &&
				record.LastHeartbeat.HasValue &&
				record.LastHeartbeat.Value < cutoff)
			{
				var updated = record with
				{
					Status = JobStatus.Queued,
					StartedAt = null,
					WorkerId = null,
					LastHeartbeat = null
				};

				if (_jobs.TryUpdate(kvp.Key, updated, record))
					reclaimed++;
			}
		}

		return Task.FromResult(reclaimed);
	}

	private static bool IsValidTransition(JobStatus from, JobStatus to) =>
		JobStatusValidator.IsValidTransition(from, to);
}
