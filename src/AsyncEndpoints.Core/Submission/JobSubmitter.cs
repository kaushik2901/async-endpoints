using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.Abstractions.Submission;
using AsyncEndpoints.Core.Infrastructure.Serialization;
using System.Text.Json.Serialization.Metadata;

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

	public async Task<Guid> SubmitAsync<T>(string jobName, T job, string? channel = null, string? partitionKey = null, CancellationToken ct = default)
	{
		var payload = SerializeJob(job);
		var descriptor = new JobDescriptor(
			JobName: jobName,
			Payload: payload,
			Channel: channel ?? "default",
			PartitionKey: partitionKey);
		return await _store.EnqueueAsync(descriptor, ct);
	}

	public Task<Guid> SubmitRawAsync(string jobName, string payload, string? channel = null, string? partitionKey = null, CancellationToken ct = default)
	{
		var descriptor = new JobDescriptor(
			JobName: jobName,
			Payload: payload,
			Channel: channel ?? "default",
			PartitionKey: partitionKey);
		return _store.EnqueueAsync(descriptor, ct);
	}

	public async Task<Guid> SubmitAsync<T>(T job, string? channel = null, string? partitionKey = null, CancellationToken ct = default)
	{
		return await SubmitAsync(typeof(T).Name, job, channel, partitionKey, ct);
	}

	private string SerializeJob<T>(T job)
	{
		JsonTypeInfo<T>? typeInfo = null;
		try
		{
			typeInfo = (JsonTypeInfo<T>)AsyncEndpointsJsonSerializationContext.Default.GetTypeInfo(typeof(T))!;
		}
		catch
		{
		}

		return typeInfo is not null
			? _serializer.Serialize(job, typeInfo)
			: _serializer.Serialize(job, (System.Text.Json.JsonSerializerOptions?)null);
	}
}
