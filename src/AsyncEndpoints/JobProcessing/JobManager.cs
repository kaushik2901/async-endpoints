using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Configuration;
using AsyncEndpoints.Extensions;
using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.JobProcessing;

/// <inheritdoc />
public class JobManager(IJobStore jobStore, ILogger<JobManager> logger, IOptions<AsyncEndpointsConfigurations> options, IDateTimeProvider dateTimeProvider) : IJobManager
{
	private readonly ILogger<JobManager> _logger = logger;
	private readonly IJobStore _jobStore = jobStore;
	private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
	private readonly AsyncEndpointsJobManagerConfiguration _jobManagerConfiguration = options.Value.JobManagerConfiguration;

	/// <inheritdoc />
	public async Task<MethodResult<Job>> SubmitJob(string jobName, string payload, HttpContext httpContext, CancellationToken cancellationToken)
	{
		using var _ = _logger.BeginScope(new { JobName = jobName });
		
		_logger.LogDebug("Processing job creation for: {JobName}, payload length: {PayloadLength}", jobName, payload.Length);

		var id = httpContext.GetOrCreateJobId();

		_logger.LogDebug("Retrieving existing job {JobId} to check if already exists", id);
		var result = await _jobStore.GetJobById(id, cancellationToken);
		if (result.IsSuccess && result.Data != null)
		{
			_logger.LogDebug("Found existing job {JobId} for job: {JobName}, returning existing job", id, jobName);
			return MethodResult<Job>.Success(result.Data);
		}

		_logger.LogDebug("Extracting context data for job {JobId}", id);
		var headers = httpContext.GetHeadersFromContext();
		var routeParams = httpContext.GetRouteParamsFromContext();
		var queryParams = httpContext.GetQueryParamsFromContext();

		_logger.LogDebug("Creating new job object with {HeaderCount} headers, {RouteParamCount} route params, {QueryParamCount} query params", 
			headers.Count, routeParams.Count, queryParams.Count);
		var job = Job.Create(id, jobName, payload, headers, routeParams, queryParams, _dateTimeProvider);
		_logger.LogDebug("Created new job {JobId} for job: {JobName}", id, jobName);

		_logger.LogDebug("Persisting job {JobId} to store", id);
		var createJobResult = await _jobStore.CreateJob(job, cancellationToken);
		if (!createJobResult.IsSuccess)
		{
			_logger.LogError("Failed to create job {JobId} in store: {Error}", id, createJobResult.Error?.Message);
			return MethodResult<Job>.Failure(createJobResult.Error!);
		}

		_logger.LogDebug("Successfully created job {JobId} in store", id);
		return MethodResult<Job>.Success(job);
	}

	/// <inheritdoc />
	public async Task<MethodResult<Job>> ClaimNextAvailableJob(Guid workerId, CancellationToken cancellationToken)
	{
		_logger.LogDebug("Attempting to claim next available job for worker {WorkerId}", workerId);
		
		var claimedJob = await _jobStore.ClaimNextJobForWorker(workerId, cancellationToken);
		
		if (claimedJob.IsSuccess)
		{
			_logger.LogDebug("Successfully claimed job {JobId} for worker {WorkerId}", claimedJob.DataOrNull?.Id, workerId);
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
