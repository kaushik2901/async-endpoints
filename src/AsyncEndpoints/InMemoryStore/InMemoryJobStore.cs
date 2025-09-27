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

    public Task<MethodResult> CreateJob(Job job, CancellationToken cancellationToken)
    {
        try
        {
            if (job == null)
            {
                _logger.LogWarning("Attempted to create null job");
                return Task.FromResult(MethodResult.Failure(
                    AsyncEndpointError.FromCode("INVALID_JOB", "Job cannot be null")));
            }

            if (job.Id == Guid.Empty)
            {
                _logger.LogWarning("Attempted to create job with empty ID");
                return Task.FromResult(MethodResult.Failure(
                    AsyncEndpointError.FromCode("INVALID_JOB_ID", "Job ID cannot be empty")));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Job create operation cancelled");
                return Task.FromCanceled<MethodResult>(cancellationToken);
            }

            if (!jobs.TryAdd(job.Id, job))
            {
                _logger.LogError("Failed to create job with ID {JobId}", job.Id);
                return Task.FromResult(MethodResult.Failure(
                    AsyncEndpointError.FromCode("JOB_CREATE_FAILED", $"Failed to create job with ID {job.Id}")));
            }

            _logger.LogInformation("Created job {JobId} with name {JobName}", job.Id, job.Name);
            return Task.FromResult(MethodResult.Success());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating job: {JobName}", job?.Name);
            return Task.FromResult(MethodResult.Failure(
                AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error creating job: {ex.Message}", ex)));
        }
    }

    public Task<MethodResult<Job>> GetJobById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            if (id == Guid.Empty)
            {
                _logger.LogWarning("Attempted to retrieve job with empty ID");
                return Task.FromResult(MethodResult<Job>.Failure(
                    AsyncEndpointError.FromCode("INVALID_JOB_ID", "Job ID cannot be empty")));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Job get operation cancelled for ID {JobId}", id);
                return Task.FromCanceled<MethodResult<Job>>(cancellationToken);
            }

            jobs.TryGetValue(id, out var job);

            if (job == null)
            {
                _logger.LogWarning("Job not found with Id {JobId} from store", id);
                return Task.FromResult(MethodResult<Job>.Failure(
                    AsyncEndpointError.FromCode("JOB_NOT_FOUND", $"Job with ID {id} not found")));
            }

            return Task.FromResult(MethodResult<Job>.Success(job));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving job: {JobId}", id);
            return Task.FromResult(MethodResult<Job>.Failure(
                AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error retrieving job: {ex.Message}", ex)));
        }
    }

    public Task<MethodResult> UpdateJob(Job job, CancellationToken cancellationToken)
    {
        try
        {
            if (job == null)
            {
                _logger.LogWarning("Attempted to update null job");
                return Task.FromResult(MethodResult.Failure(
                    AsyncEndpointError.FromCode("INVALID_JOB", "Job cannot be null")));
            }

            if (job.Id == Guid.Empty)
            {
                _logger.LogWarning("Attempted to update job with empty ID");
                return Task.FromResult(MethodResult.Failure(
                    AsyncEndpointError.FromCode("INVALID_JOB_ID", "Job ID cannot be empty")));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Job update operation cancelled for ID {JobId}", job.Id);
                return Task.FromCanceled<MethodResult>(cancellationToken);
            }

            if (!jobs.TryGetValue(job.Id, out var existingJob))
            {
                _logger.LogWarning("Attempted to update non-existent job {JobId}", job.Id);
                return Task.FromResult(MethodResult.Failure(
                    AsyncEndpointError.FromCode("JOB_NOT_FOUND", $"Job with ID {job.Id} not found")));
            }

            // Update the job in the store
            jobs[job.Id] = job;
            job.LastUpdatedAt = DateTimeOffset.UtcNow;

            _logger.LogDebug("Updated job {JobId}", job.Id);
            return Task.FromResult(MethodResult.Success());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating job: {JobId}", job?.Id);
            return Task.FromResult(MethodResult.Failure(
                AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error updating job: {ex.Message}", ex)));
        }
    }

    public Task<MethodResult<List<Job>>> ClaimJobsForWorker(Guid workerId, int maxClaimCount, CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Claim jobs for worker operation cancelled");
                return Task.FromCanceled<MethodResult<List<Job>>>(cancellationToken);
            }

            var now = DateTime.UtcNow;

            var availableJobs = jobs.Values
                .Where(job => job.WorkerId == null)
                .Where(job => (
                    job.Status == JobStatus.Queued ||
                    (job.Status == JobStatus.Scheduled &&
                    (job.RetryDelayUntil == null || job.RetryDelayUntil <= now)))
                )
                .Take(maxClaimCount)
                .ToList();

            foreach (var job in availableJobs)
            {
                job.WorkerId = workerId;
            }

            _logger.LogInformation("Claimed {Count} jobs for worker {WorkerId}", availableJobs.Count, workerId);
            return Task.FromResult(MethodResult<List<Job>>.Success(availableJobs));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error claiming jobs for worker {WorkerId}", workerId);
            return Task.FromResult(MethodResult<List<Job>>.Failure(
                AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error claiming jobs: {ex.Message}", ex)));
        }
    }
}
