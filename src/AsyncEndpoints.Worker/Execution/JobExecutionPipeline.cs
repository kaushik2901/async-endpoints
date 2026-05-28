using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.Core.Execution;
using AsyncEndpoints.Worker.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.Worker.Execution;

public sealed class JobExecutionPipeline
{
	private readonly JobDispatcher _dispatcher;
	private readonly IJobStore _store;
	private readonly RetryHandler _retryHandler;
	private readonly ILogger<JobExecutionPipeline> _logger;
	private readonly WorkerOptions _options;

	public JobExecutionPipeline(
		JobDispatcher dispatcher,
		IJobStore store,
		RetryHandler retryHandler,
		IOptions<WorkerOptions> options,
		ILogger<JobExecutionPipeline> logger)
	{
		_dispatcher = dispatcher;
		_store = store;
		_retryHandler = retryHandler;
		_logger = logger;
		_options = options.Value;
	}

	public async Task ExecuteAsync(JobRecord record, CancellationToken ct)
	{
		try
		{
			var result = await _dispatcher.DispatchAsync(record, ct);

			if (result.IsSuccess)
			{
				_logger.LogInformation("Job {JobId} completed successfully", record.JobId);
				await _store.UpdateStatusAsync(record.JobId, JobStatus.Completed, result.Result, ct);
			}
			else
			{
				await HandleFailureAsync(record, result.ErrorMessage, ct);
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogCritical(ex, "Unexpected error in execution pipeline for job {JobId}", record.JobId);
		}
	}

	private async Task HandleFailureAsync(JobRecord record, string? errorMessage, CancellationToken ct)
	{
		var updatedRecord = record with
		{
			RetryCount = record.RetryCount + 1,
			ErrorMessage = errorMessage
		};

		if (_retryHandler.ShouldRetry(updatedRecord))
		{
			_logger.LogWarning(
				"Job {JobId} failed (attempt {RetryCount}/{MaxRetries}), re-queuing. Error: {Error}",
				record.JobId, updatedRecord.RetryCount, Math.Max(record.MaxRetries, _options.MaxRetries), errorMessage);

			await _store.UpdateStatusAsync(record.JobId, JobStatus.Queued, errorMessage, ct);
		}
		else
		{
			_logger.LogError(
				"Job {JobId} failed after {RetryCount} attempts, moving to dead letter. Error: {Error}",
				record.JobId, updatedRecord.RetryCount, errorMessage);

			await _store.UpdateStatusAsync(record.JobId, JobStatus.DeadLettered, errorMessage, ct);
		}
	}
}
