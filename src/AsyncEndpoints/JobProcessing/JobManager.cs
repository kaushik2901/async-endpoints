using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Configuration;
using AsyncEndpoints.Extensions;
using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.Infrastructure.Observability;
using AsyncEndpoints.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.JobProcessing;

/// <inheritdoc />
public class JobManager(IJobStore jobStore, ILogger<JobManager> logger, IOptions<AsyncEndpointsConfigurations> options, IDateTimeProvider dateTimeProvider, IAsyncEndpointsObservability metrics) : IJobManager
{
	private readonly ILogger<JobManager> _logger = logger;
	private readonly IJobStore _jobStore = jobStore;
	private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
	private readonly AsyncEndpointsJobManagerConfiguration _jobManagerConfiguration = options.Value.JobManagerConfiguration;
	private readonly IAsyncEndpointsObservability _metrics = metrics;

	/// <inheritdoc />
	public async Task<MethodResult<Job>> SubmitJob(string jobName, string payload, HttpContext httpContext, CancellationToken cancellationToken)
	{
		using var _ = _logger.BeginScope(new { JobName = jobName });
		
		var id = httpContext.GetOrCreateJobId();
		
		// Start activity only if tracing is enabled
		using var activity = _metrics.StartJobSubmitActivity(jobName, _jobStore.GetType().Name, id);
		
		// Use disposable timer to measure total duration
		using var durationTimer = _metrics.TimeJobProcessingDuration(jobName, "created");
		
		_logger.LogDebug("Processing job creation for: {JobName}, payload length: {PayloadLength}", jobName, payload.Length);

		var result = await _jobStore.GetJobById(id, cancellationToken);
		if (result.IsSuccess && result.DataOrNull != null)
		{
			_logger.LogDebug("Found existing job {JobId} for job: {JobName}, returning existing job", id, jobName);
			return MethodResult<Job>.Success(result.Data);
		}

		var headers = httpContext.GetHeadersFromContext();
		var routeParams = httpContext.GetRouteParamsFromContext();
		var queryParams = httpContext.GetQueryParamsFromContext();

		var job = Job.Create(id, jobName, payload, headers, routeParams, queryParams, _dateTimeProvider);
		var createJobResult = await _jobStore.CreateJob(job, cancellationToken);
		if (createJobResult.IsSuccess)
		{
			_metrics.RecordJobCreated(jobName, _jobStore.GetType().Name);
			
			return MethodResult<Job>.Success(job);
		}
		else
		{
			_logger.LogError("Failed to create job {JobId} in store: {Error}", id, createJobResult.Error?.Message);
			return MethodResult<Job>.Failure(createJobResult.Error!);
		}
	}

	/// <inheritdoc />
	public async Task<MethodResult<Job>> ClaimNextAvailableJob(Guid workerId, CancellationToken cancellationToken)
	{
		_logger.LogDebug("Attempting to claim next available job for worker {WorkerId}", workerId);
		
		var claimedJob = await _jobStore.ClaimNextJobForWorker(workerId, cancellationToken);
		
		if (claimedJob.IsSuccess)
		{
			_logger.LogDebug("Successfully claimed job {JobId} for worker {WorkerId}", claimedJob.DataOrNull?.Id, workerId);
			if (claimedJob.DataOrNull != null)
			{
				_metrics.RecordJobProcessed(claimedJob.DataOrNull.Name, "claimed", _jobStore.GetType().Name);
			}
		}
		else if (claimedJob.IsSuccess && claimedJob.DataOrNull == null)
		{
			_logger.LogDebug("No available jobs to claim for worker {WorkerId}", workerId);
		}
		else
		{
			_logger.LogError("Failed to claim job for worker {WorkerId}: {Error}", workerId, claimedJob.Error?.Message);
		}
		
		return claimedJob;
	}

	/// <inheritdoc />
	public async Task<MethodResult> ProcessJobSuccess(Guid jobId, string result, CancellationToken cancellationToken)
	{
		using var _ = _logger.BeginScope(new { JobId = jobId });
		
		_logger.LogDebug("Processing successful completion for job {JobId}", jobId);
		
		var jobResult = await _jobStore.GetJobById(jobId, cancellationToken);
		if (!jobResult.IsSuccess || jobResult.Data == null)
		{
			_logger.LogError("Job {JobId} not found when processing success", jobId);
			return MethodResult.Failure(new AsyncEndpointError("JOB_NOT_FOUND", $"Job {jobId} not found"));
		}

		var job = jobResult.Data;
		_logger.LogDebug("Setting result for job {JobId}, result length: {ResultLength}", jobId, result.Length);

		job.SetResult(result, _dateTimeProvider);

		var updateResult = await _jobStore.UpdateJob(job, cancellationToken);
		if (updateResult.IsSuccess)
		{
			_metrics.RecordJobProcessed(job.Name, "completed", _jobStore.GetType().Name);
			var duration = (job.CompletedAt?.UtcDateTime - job.CreatedAt.UtcDateTime).GetValueOrDefault().TotalSeconds;
			_metrics.RecordJobProcessingDuration(job.Name, "completed", duration);
			
			_logger.LogInformation("Successfully processed job {JobId} completion", jobId);
		}
		else
		{
			_logger.LogError("Failed to update job {JobId} after processing success: {Error}", jobId, updateResult.Error?.Message);
		}

		return updateResult;
	}

	/// <inheritdoc />
	public async Task<MethodResult> ProcessJobFailure(Guid jobId, AsyncEndpointError error, CancellationToken cancellationToken)
	{
		using var _ = _logger.BeginScope(new { JobId = jobId, Error = error.Code });
		
		_logger.LogDebug("Processing failure for job {JobId}, error code: {ErrorCode}", jobId, error.Code);
		
		var jobResult = await _jobStore.GetJobById(jobId, cancellationToken);
		if (!jobResult.IsSuccess || jobResult.Data == null)
		{
			_logger.LogError("Job {JobId} not found when processing failure", jobId);
			return MethodResult.Failure(new AsyncEndpointError("JOB_NOT_FOUND", $"Job {jobId} not found"));
		}

		var job = jobResult.Data;
		_logger.LogDebug("Current retry count for job {JobId}: {RetryCount}/{MaxRetries}", jobId, job.RetryCount, job.MaxRetries);

		// Check if retry is possible
		if (job.RetryCount < job.MaxRetries)
		{
			job.IncrementRetryCount();
			_metrics.RecordJobRetries(job.Name, _jobStore.GetType().Name);
			var retryDelay = CalculateRetryDelay(job.RetryCount);
			job.SetRetryTime(_dateTimeProvider.UtcNow.Add(retryDelay));
			job.UpdateStatus(JobStatus.Scheduled, _dateTimeProvider);
			job.WorkerId = null; // Release from current worker
			job.Error = error;

			_logger.LogDebug("Scheduled retry for job {JobId} in {RetryDelay}s, retry count: {RetryCount}", 
				jobId, retryDelay.TotalSeconds, job.RetryCount);
		}
		else
		{
			job.SetError(error, _dateTimeProvider);
			_metrics.RecordJobFailed(job.Name, error.Code, _jobStore.GetType().Name);
			_logger.LogDebug("Max retries exceeded for job {JobId}, setting final error status", jobId);
		}

		var updateResult = await _jobStore.UpdateJob(job, cancellationToken);
		if (updateResult.IsSuccess)
		{
			_logger.LogInformation("Successfully processed job {JobId} failure", jobId);
		}
		else
		{
			_logger.LogError("Failed to update job {JobId} after processing failure: {Error}", jobId, updateResult.Error?.Message);
		}

		return updateResult;
	}

	/// <inheritdoc />
	public async Task<MethodResult<Job>> GetJobById(Guid jobId, CancellationToken cancellationToken)
	{
		return await _jobStore.GetJobById(jobId, cancellationToken);
	}

	private TimeSpan CalculateRetryDelay(int retryCount)
	{
		// Exponential backoff: (2 ^ retryCount) * base delay
		return TimeSpan.FromSeconds(Math.Pow(2, retryCount) * _jobManagerConfiguration.RetryDelayBaseSeconds);
	}
}
