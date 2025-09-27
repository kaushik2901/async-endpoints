using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Contracts;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.Services;

public class JobManager(IJobStore jobStore, ILogger<JobManager> logger, IOptions<AsyncEndpointsConfigurations> options) : IJobManager
{
    private readonly ILogger<JobManager> _logger = logger;
    private readonly IJobStore _jobStore = jobStore;
    private readonly AsyncEndpointsJobManagerConfiguration _jobManagerConfiguration = options.Value.JobManagerConfiguration;

    public async Task<MethodResult<Job>> SubmitJob(string jobName, string payload, HttpContext httpContext, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing job creation for: {JobName}", jobName);

        var id = httpContext.GetOrCreateJobId();

        var result = await _jobStore.GetJobById(id, cancellationToken);
        if (result.IsSuccess && result.Data != null)
        {
            _logger.LogDebug("Found existing job {JobId} for job: {JobName}", id, jobName);
            return MethodResult<Job>.Success(result.Data);
        }

        var headers = httpContext.GetHeadersFromContext();
        var routeParams = httpContext.GetRouteParamsFromContext();
        var queryParams = httpContext.GetQueryParamsFromContext();

        var job = Job.Create(id, jobName, payload, headers, routeParams, queryParams);
        _logger.LogDebug("Creating new job {JobId} for job: {JobName}", id, jobName);

        var createJobResult = await _jobStore.CreateJob(job, cancellationToken);
        if (!createJobResult.IsSuccess)
        {
            return MethodResult<Job>.Failure(createJobResult.Error!);
        }

        return MethodResult<Job>.Success(job);
    }

    public async Task<MethodResult<List<Job>>> ClaimJobsForProcessing(Guid workerId, int maxClaimCount, CancellationToken cancellationToken)
    {
        var availableJobs = await _jobStore.ClaimJobsForWorker(workerId, maxClaimCount, cancellationToken);

        if (availableJobs.IsSuccess && availableJobs.Data?.Count == 0)
        {
            foreach (var job in availableJobs.Data)
            {
                job.UpdateStatus(JobStatus.InProgress);
                job.WorkerId = workerId;
                await _jobStore.UpdateJob(job, cancellationToken);
            }
        }

        return availableJobs;
    }

    public async Task<MethodResult> ProcessJobSuccess(Guid jobId, string result, CancellationToken cancellationToken)
    {
        var jobResult = await _jobStore.GetJobById(jobId, cancellationToken);
        if (!jobResult.IsSuccess || jobResult.Data == null)
            return MethodResult.Failure(new AsyncEndpointError("JOB_NOT_FOUND", $"Job {jobId} not found"));

        var job = jobResult.Data;

        job.SetResult(result);

        return await _jobStore.UpdateJob(job, cancellationToken);
    }

    public async Task<MethodResult> ProcessJobFailure(Guid jobId, string exception, CancellationToken cancellationToken)
    {
        var jobResult = await _jobStore.GetJobById(jobId, cancellationToken);
        if (!jobResult.IsSuccess || jobResult.Data == null)
            return MethodResult.Failure(new AsyncEndpointError("JOB_NOT_FOUND", $"Job {jobId} not found"));

        var job = jobResult.Data;

        // Check if retry is possible
        if (job.RetryCount < job.MaxRetries)
        {
            job.IncrementRetryCount();
            var retryDelay = CalculateRetryDelay(job.RetryCount);
            job.SetRetryTime(DateTime.UtcNow.Add(retryDelay));
            job.UpdateStatus(JobStatus.Scheduled);
            job.WorkerId = null; // Release from current worker
            job.Exception = exception;
        }
        else
        {
            job.SetException(exception);
        }

        return await _jobStore.UpdateJob(job, cancellationToken);
    }

    private TimeSpan CalculateRetryDelay(int retryCount)
    {
        // Exponential backoff: (2 ^ retryCount) * base delay
        return TimeSpan.FromSeconds(Math.Pow(2, retryCount) * _jobManagerConfiguration.RetryDelayBaseSeconds);
    }
}