using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Listener;
using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.Abstractions.Submission;
using AsyncEndpoints.Core.Serialization;

namespace AsyncEndpoints.Core.Submission;

/// <summary>
/// Implementation of IJobSubmitter that handles job serialization and storage.
/// </summary>
public class JobSubmitter : IJobSubmitter
{
	private readonly IJobStore _store;
	private readonly JobSerializerRegistry _serializerRegistry;
	private readonly IJobNotifier? _notifier;

	public JobSubmitter(IJobStore store, JobSerializerRegistry serializerRegistry, IJobNotifier? notifier = null)
	{
		_store = store;
		_serializerRegistry = serializerRegistry;
		_notifier = notifier;
	}

	public async Task<JobRecord> SubmitAsync<TJob>(
		TJob job,
		string? channel = null,
		int? priority = null,
		string? partitionBy = null,
		CancellationToken ct = default) where TJob : notnull
	{
		var jobId = Guid.NewGuid();
		var payloadType = typeof(TJob).Name;
		var payload = _serializerRegistry.Serialize(job);

		var descriptor = new JobDescriptor
		{
			JobId = jobId,
			Channel = channel ?? "default",
			Priority = priority ?? 0,
			PayloadType = payloadType,
			Payload = payload,
			PartitionKey = partitionBy,
			MaxRetries = 3 // Default, could be configurable
		};

		await _store.EnqueueAsync(descriptor, ct);

		if (_notifier != null)
		{
			await _notifier.NotifyJobAvailableAsync(descriptor.Channel, ct);
		}

		// We return a skeleton JobRecord representing what was submitted.
		// The store might have added its own metadata, but this is the initial state.
		return new JobRecord
		{
			JobId = jobId,
			Channel = descriptor.Channel,
			Priority = descriptor.Priority,
			PayloadType = payloadType,
			Payload = payload,
			Status = JobStatus.Queued,
			PartitionKey = partitionBy,
			MaxRetries = descriptor.MaxRetries,
			CreatedAt = DateTimeOffset.UtcNow
		};
	}
}
