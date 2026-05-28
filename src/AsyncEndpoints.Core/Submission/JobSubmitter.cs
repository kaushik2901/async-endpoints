using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.Abstractions.Submission;

namespace AsyncEndpoints.Core.Submission;

public sealed class JobSubmitter : IJobSubmitter
{
	private readonly IJobStore _store;

	public JobSubmitter(IJobStore store)
	{
		_store = store;
	}

	public Task<Guid> SubmitAsync(string jobName, string payload, string? channel = null, string? partitionKey = null, CancellationToken ct = default)
	{
		var descriptor = new JobDescriptor(
			JobName: jobName,
			Payload: payload,
			Channel: channel ?? "default",
			PartitionKey: partitionKey);
		return _store.EnqueueAsync(descriptor, ct);
	}
}
