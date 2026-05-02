using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Core.Internal;
using AsyncEndpoints.Core.Serialization;

namespace AsyncEndpoints.Core.Execution;

/// <summary>
/// Dispatches jobs to their respective handlers.
/// </summary>
public class JobDispatcher
{
	private readonly IServiceProvider _serviceProvider;
	private readonly JobTypeRegistry _typeRegistry;
	private readonly JobSerializerRegistry _serializerRegistry;

	public JobDispatcher(
		IServiceProvider serviceProvider,
		JobTypeRegistry typeRegistry,
		JobSerializerRegistry serializerRegistry)
	{
		_serviceProvider = serviceProvider;
		_typeRegistry = typeRegistry;
		_serializerRegistry = serializerRegistry;
	}

	/// <summary>
	/// Deserializes and executes a job based on the record.
	/// </summary>
	public virtual async Task DispatchAsync(JobRecord record, CancellationToken ct)
	{
		// 1. Get executor (AOT-safe resolution)
		var executor = _typeRegistry.GetExecutor(record.PayloadType, _serviceProvider);

		// 2. Deserialize payload
		var payload = _serializerRegistry.Deserialize(record.PayloadType, record.Payload);

		// 3. Execute
		await executor.ExecuteAsync(record, payload, ct);
	}
}
