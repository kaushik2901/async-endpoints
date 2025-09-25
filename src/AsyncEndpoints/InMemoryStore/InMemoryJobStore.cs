using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Contracts;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Utilities;

namespace AsyncEndpoints.InMemoryStore;

public class InMemoryJobStore : IJobStore
{
    private readonly ConcurrentDictionary<Guid, Job> jobs = new();

    public Task<MethodResult> Add(Job job, CancellationToken cancellationToken)
    {
        try
        {
            if (job == null)
                return Task.FromResult(MethodResult.Failure(
                    AsyncEndpointError.FromCode("INVALID_JOB", "Job cannot be null")));

            if (job.Id == Guid.Empty)
                return Task.FromResult(MethodResult.Failure(
                    AsyncEndpointError.FromCode("INVALID_JOB_ID", "Job ID cannot be empty")));

            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<MethodResult>(cancellationToken);

            if (!jobs.TryAdd(job.Id, job))
                return Task.FromResult(MethodResult.Failure(
                    AsyncEndpointError.FromCode("JOB_ADD_FAILED", $"Failed to add job with ID {job.Id}")));

            return Task.FromResult(MethodResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(MethodResult.Failure(
                AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error adding job: {ex.Message}", ex)));
        }
    }

    public Task<MethodResult<Job?>> Get(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            if (id == Guid.Empty)
                return Task.FromResult(MethodResult<Job?>.Failure(
                    AsyncEndpointError.FromCode("INVALID_JOB_ID", "Job ID cannot be empty")));

            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<MethodResult<Job?>>(cancellationToken);

            jobs.TryGetValue(id, out var job);

            return Task.FromResult(MethodResult<Job?>.Success(job));
        }
        catch (Exception ex)
        {
            return Task.FromResult(MethodResult<Job?>.Failure(
                AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error retrieving job: {ex.Message}", ex)));
        }
    }

    public Task<MethodResult<List<Job>>> GetQueuedJobs(Guid workerId, int maxSize, CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<MethodResult<List<Job>>>(cancellationToken);

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

            return Task.FromResult(MethodResult<List<Job>>.Success(availableJobs));
        }
        catch (Exception ex)
        {
            return Task.FromResult(MethodResult<List<Job>>.Failure(
                AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error retrieving jobs by status: {ex.Message}", ex)));
        }
    }

    public Task<MethodResult> UpdateJobStatus(Guid id, JobStatus status, CancellationToken cancellationToken)
    {
        try
        {
            if (id == Guid.Empty)
                return Task.FromResult(MethodResult.Failure(
                    AsyncEndpointError.FromCode("INVALID_JOB_ID", "Job ID cannot be empty")));

            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<MethodResult>(cancellationToken);

            if (!jobs.TryGetValue(id, out var existingJob))
                return Task.FromResult(MethodResult.Failure(
                    AsyncEndpointError.FromCode("JOB_NOT_FOUND", $"Job with ID {id} not found")));

            existingJob.UpdateStatus(status);

            if (status == JobStatus.Queued || status == JobStatus.Scheduled)
            {
                existingJob.WorkerId = null;
            }

            return Task.FromResult(MethodResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(MethodResult.Failure(
                AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error updating job status: {ex.Message}", ex)));
        }
    }

    public Task<MethodResult> UpdateJobResult(Guid id, string result, CancellationToken cancellationToken)
    {
        try
        {
            if (id == Guid.Empty)
                return Task.FromResult(MethodResult.Failure(
                    AsyncEndpointError.FromCode("INVALID_JOB_ID", "Job ID cannot be empty")));

            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<MethodResult>(cancellationToken);

            if (!jobs.TryGetValue(id, out var existingJob))
                return Task.FromResult(MethodResult.Failure(
                    AsyncEndpointError.FromCode("JOB_NOT_FOUND", $"Job with ID {id} not found")));

            existingJob.SetResult(result);

            return Task.FromResult(MethodResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(MethodResult.Failure(
                AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error updating job status: {ex.Message}", ex)));
        }
    }

    public Task<MethodResult> UpdateJobException(Guid id, string exception, CancellationToken cancellationToken)
    {
        try
        {
            if (id == Guid.Empty)
                return Task.FromResult(MethodResult.Failure(
                    AsyncEndpointError.FromCode("INVALID_JOB_ID", "Job ID cannot be empty")));

            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<MethodResult>(cancellationToken);

            if (!jobs.TryGetValue(id, out var existingJob))
                return Task.FromResult(MethodResult.Failure(
                    AsyncEndpointError.FromCode("JOB_NOT_FOUND", $"Job with ID {id} not found")));

            if (existingJob.RetryCount < existingJob.MaxRetries)
            {
                existingJob.IncrementRetryCount();

                var retryDelay = TimeSpan.FromSeconds(Math.Pow(2, existingJob.RetryCount) * 5);
                existingJob.SetRetryTime(DateTime.UtcNow.Add(retryDelay));

                existingJob.WorkerId = null;
                existingJob.UpdateStatus(JobStatus.Scheduled);

                existingJob.Exception = exception;
            }
            else
            {
                existingJob.SetException(exception);
            }

            return Task.FromResult(MethodResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(MethodResult.Failure(
                AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error updating job status: {ex.Message}", ex)));
        }
    }
}
