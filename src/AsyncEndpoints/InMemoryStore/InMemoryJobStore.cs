using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Contracts;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.InMemoryStore;

/// <summary>
/// An in-memory implementation of the IJobStore interface.
/// This implementation stores jobs in a thread-safe concurrent dictionary and is suitable for development or single-instance deployments.
/// </summary>
public class InMemoryJobStore(ILogger<InMemoryJobStore> logger) : IJobStore
{
    private readonly ILogger<InMemoryJobStore> _logger = logger;
    private readonly ConcurrentDictionary<Guid, Job> jobs = new();

    public Task<MethodResult> Add(Job job, CancellationToken cancellationToken)
    {
        try
        {
            if (job == null)
            {
                _logger.LogWarning("Attempted to add null job");
                return Task.FromResult(MethodResult.Failure(
                    AsyncEndpointError.FromCode("INVALID_JOB", "Job cannot be null")));
            }

            if (job.Id == Guid.Empty)
            {
                _logger.LogWarning("Attempted to add job with empty ID");
                return Task.FromResult(MethodResult.Failure(
                    AsyncEndpointError.FromCode("INVALID_JOB_ID", "Job ID cannot be empty")));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Job add operation cancelled");
                return Task.FromCanceled<MethodResult>(cancellationToken);
            }

            if (!jobs.TryAdd(job.Id, job))
            {
                _logger.LogError("Failed to add job with ID {JobId}", job.Id);
                return Task.FromResult(MethodResult.Failure(
                    AsyncEndpointError.FromCode("JOB_ADD_FAILED", $"Failed to add job with ID {job.Id}")));
            }

            _logger.LogInformation("Added job {JobId} with name {JobName}", job.Id, job.Name);
            return Task.FromResult(MethodResult.Success());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error adding job: {JobName}", job?.Name);
            return Task.FromResult(MethodResult.Failure(
                AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error adding job: {ex.Message}", ex)));
        }
    }

    public Task<MethodResult<Job?>> Get(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            if (id == Guid.Empty)
            {
                _logger.LogWarning("Attempted to retrieve job with empty ID");
                return Task.FromResult(MethodResult<Job?>.Failure(
                    AsyncEndpointError.FromCode("INVALID_JOB_ID", "Job ID cannot be empty")));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Job get operation cancelled for ID {JobId}", id);
                return Task.FromCanceled<MethodResult<Job?>>(cancellationToken);
            }

            jobs.TryGetValue(id, out var job);

            if (job != null)
            {
                _logger.LogDebug("Retrieved job {JobId} from store", id);
            }
            else
            {
                _logger.LogDebug("Job {JobId} not found in store", id);
            }

            return Task.FromResult(MethodResult<Job?>.Success(job));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving job: {JobId}", id);
            return Task.FromResult(MethodResult<Job?>.Failure(
                AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error retrieving job: {ex.Message}", ex)));
        }
    }

    public Task<MethodResult<List<Job>>> GetQueuedJobs(Guid workerId, int maxSize, CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Get queued jobs operation cancelled");
                return Task.FromCanceled<MethodResult<List<Job>>>(cancellationToken);
            }

            var now = DateTime.UtcNow;
            List<JobStatus> statuses = [JobStatus.Queued, JobStatus.Scheduled];

            var availableJobs = jobs.Values
                .Where(job => job.WorkerId == null)
                .Where(job => (
                    job.Status == JobStatus.Queued ||
                    (job.Status == JobStatus.Scheduled &&
                    (job.RetryDelayUntil == null || job.RetryDelayUntil <= now)))
                )
                .Take(maxSize)
                .ToList();

            foreach (var job in availableJobs)
            {
                job.WorkerId = workerId;
            }

            _logger.LogInformation("Retrieved {Count} queued jobs for worker {WorkerId}", availableJobs.Count, workerId);
            return Task.FromResult(MethodResult<List<Job>>.Success(availableJobs));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving queued jobs for worker {WorkerId}", workerId);
            return Task.FromResult(MethodResult<List<Job>>.Failure(
                AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error retrieving jobs by status: {ex.Message}", ex)));
        }
    }

    public Task<MethodResult> UpdateJobStatus(Guid id, JobStatus status, CancellationToken cancellationToken)
    {
        try
        {
            if (id == Guid.Empty)
            {
                _logger.LogWarning("Attempted to update job with empty ID");
                return Task.FromResult(MethodResult.Failure(
                    AsyncEndpointError.FromCode("INVALID_JOB_ID", "Job ID cannot be empty")));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Job status update operation cancelled for ID {JobId}", id);
                return Task.FromCanceled<MethodResult>(cancellationToken);
            }

            if (!jobs.TryGetValue(id, out var existingJob))
            {
                _logger.LogWarning("Attempted to update status of non-existent job {JobId}", id);
                return Task.FromResult(MethodResult.Failure(
                    AsyncEndpointError.FromCode("JOB_NOT_FOUND", $"Job with ID {id} not found")));
            }

            var oldStatus = existingJob.Status;
            existingJob.UpdateStatus(status);

            if (status == JobStatus.Queued || status == JobStatus.Scheduled)
            {
                existingJob.WorkerId = null;
            }

            _logger.LogInformation("Updated job {JobId} status from {OldStatus} to {NewStatus}", id, oldStatus, status);
            return Task.FromResult(MethodResult.Success());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating job {JobId} status to {Status}", id, status);
            return Task.FromResult(MethodResult.Failure(
                AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error updating job status: {ex.Message}", ex)));
        }
    }

    public Task<MethodResult> UpdateJobResult(Guid id, string result, CancellationToken cancellationToken)
    {
        try
        {
            if (id == Guid.Empty)
            {
                _logger.LogWarning("Attempted to update result of job with empty ID");
                return Task.FromResult(MethodResult.Failure(
                    AsyncEndpointError.FromCode("INVALID_JOB_ID", "Job ID cannot be empty")));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Job result update operation cancelled for ID {JobId}", id);
                return Task.FromCanceled<MethodResult>(cancellationToken);
            }

            if (!jobs.TryGetValue(id, out var existingJob))
            {
                _logger.LogWarning("Attempted to update result of non-existent job {JobId}", id);
                return Task.FromResult(MethodResult.Failure(
                    AsyncEndpointError.FromCode("JOB_NOT_FOUND", $"Job with ID {id} not found")));
            }

            existingJob.SetResult(result);

            _logger.LogInformation("Updated result for job {JobId}", id);
            return Task.FromResult(MethodResult.Success());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating job {JobId} result", id);
            return Task.FromResult(MethodResult.Failure(
                AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error updating job status: {ex.Message}", ex)));
        }
    }

    public Task<MethodResult> UpdateJobException(Guid id, string exception, CancellationToken cancellationToken)
    {
        try
        {
            if (id == Guid.Empty)
            {
                _logger.LogWarning("Attempted to update exception of job with empty ID");
                return Task.FromResult(MethodResult.Failure(
                    AsyncEndpointError.FromCode("INVALID_JOB_ID", "Job ID cannot be empty")));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Job exception update operation cancelled for ID {JobId}", id);
                return Task.FromCanceled<MethodResult>(cancellationToken);
            }

            if (!jobs.TryGetValue(id, out var existingJob))
            {
                _logger.LogWarning("Attempted to update exception of non-existent job {JobId}", id);
                return Task.FromResult(MethodResult.Failure(
                    AsyncEndpointError.FromCode("JOB_NOT_FOUND", $"Job with ID {id} not found")));
            }

            if (existingJob.RetryCount < existingJob.MaxRetries)
            {
                existingJob.IncrementRetryCount();
                _logger.LogDebug("Job {JobId} retry count incremented to {RetryCount}", id, existingJob.RetryCount);

                var retryDelay = TimeSpan.FromSeconds(Math.Pow(2, existingJob.RetryCount) * AsyncEndpointsConstants.RetryDelayBaseSeconds);
                existingJob.SetRetryTime(DateTime.UtcNow.Add(retryDelay));

                existingJob.WorkerId = null;
                existingJob.UpdateStatus(JobStatus.Scheduled);

                existingJob.Exception = exception;

                _logger.LogInformation("Scheduled retry for job {JobId}, attempt {RetryCount}/{MaxRetries}",
                    id, existingJob.RetryCount, existingJob.MaxRetries);
            }
            else
            {
                existingJob.SetException(exception);
                _logger.LogInformation("Job {JobId} failed after {RetryCount} retries, marking as failed",
                    id, existingJob.MaxRetries);
            }

            return Task.FromResult(MethodResult.Success());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating exception for job {JobId}", id);
            return Task.FromResult(MethodResult.Failure(
                AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error updating job status: {ex.Message}", ex)));
        }
    }
}
