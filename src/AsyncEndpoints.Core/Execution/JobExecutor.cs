using AsyncEndpoints.Abstractions.Jobs;

namespace AsyncEndpoints.Core.Execution;

/// <summary>
/// Non-generic interface for job execution.
/// </summary>
public interface IJobExecutor
{
	/// <summary>
	/// Executes the job with the given payload.
	/// </summary>
	Task ExecuteAsync(JobRecord record, object payload, CancellationToken ct);
}

/// <summary>
/// Generic implementation of job executor that wraps an IJobHandler.
/// </summary>
public class JobExecutor<TJob> : IJobExecutor
{
	private readonly IJobHandler<TJob> _handler;

	public JobExecutor(IJobHandler<TJob> handler)
	{
		_handler = handler;
	}

	public Task ExecuteAsync(JobRecord record, object payload, CancellationToken ct)
	{
		if (payload is not TJob typedPayload)
		{
			throw new System.ArgumentException($"Payload is not of type {typeof(TJob).Name}", nameof(payload));
		}
		return _handler.HandleAsync(typedPayload, ct);
	}
}
