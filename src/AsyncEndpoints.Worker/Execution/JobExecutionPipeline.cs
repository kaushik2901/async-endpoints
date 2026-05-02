using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.Core.Execution;
using AsyncEndpoints.Worker.Concurrency;

namespace AsyncEndpoints.Worker.Execution;

/// <summary>
/// Manages the execution lifecycle of a single job.
/// </summary>
public class JobExecutionPipeline
{
	private readonly IJobStore _store;
	private readonly JobDispatcher _dispatcher;
	private readonly RetryHandler _retryHandler;
	private readonly WorkerConcurrencyManager _concurrencyManager;

	public JobExecutionPipeline(
		IJobStore store,
		JobDispatcher dispatcher,
		RetryHandler retryHandler,
		WorkerConcurrencyManager concurrencyManager)
	{
		_store = store;
		_dispatcher = dispatcher;
		_retryHandler = retryHandler;
		_concurrencyManager = concurrencyManager;
	}

	/// <summary>
	/// Executes the job lifecycle: Dequeue (already done) -> Execute -> UpdateStatus.
	/// </summary>
	public virtual async Task RunAsync(JobRecord record, CancellationToken ct)
	{
		try
		{
			// 1. Track job for heartbeating
			_concurrencyManager.RegisterJob(record.JobId);

			// 2. Move to Processing
			await _store.UpdateStatusAsync(record.JobId, JobStatus.Processing, ct: ct);

			// 3. Dispatch to handler
			await _dispatcher.DispatchAsync(record, ct);

			// 4. Mark as Completed
			await _store.UpdateStatusAsync(record.JobId, JobStatus.Completed, ct: ct);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			// Job execution was cancelled (graceful shutdown)
		}
		catch (Exception ex)
		{
			// 5. Move to Failed state first to satisfy state machine (Processing -> Failed)
			await _store.UpdateStatusAsync(record.JobId, JobStatus.Failed, error: ex.ToString(), ct: ct);

			// 6. Handle failure (Retry or Dead-letter) (Failed -> Queued/DeadLettered)
			await _retryHandler.HandleFailureAsync(record, ex, ct);
		}
		finally
		{
			// 6. Release concurrency slot
			_concurrencyManager.ReleaseJob(record.JobId);
		}
	}
}
