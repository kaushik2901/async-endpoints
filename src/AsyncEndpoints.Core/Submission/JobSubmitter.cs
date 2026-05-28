using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.Abstractions.Submission;
using AsyncEndpoints.Core.Infrastructure.Serialization;

namespace AsyncEndpoints.Core.Submission;

public sealed class JobSubmitter : IJobSubmitter
{
	private readonly IJobStore _store;
	private readonly ISerializer _serializer;

	public JobSubmitter(IJobStore store, ISerializer serializer)
	{
		_store = store;
		_serializer = serializer;
	}

	public async Task<Guid> SubmitAsync<T>(T job, string? channel = null, string? partitionKey = null, CancellationToken ct = default)
	{
		var payload = _serializer.Serialize(job);
		var descriptor = new JobDescriptor(
			JobName: typeof(T).Name,
			Payload: payload,
			Channel: channel ?? "default",
			PartitionKey: partitionKey);
		return await _store.EnqueueAsync(descriptor, ct);
	}
}
