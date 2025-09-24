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

    public Task<MethodResult<List<Job>>> GetByStatus(JobStatus status, int maxSize, CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<MethodResult<List<Job>>>(cancellationToken);

            var jobsWithStatus = jobs.Values
                .Where(job => job.Status == status)
                .Take(maxSize)
                .ToList();

            return Task.FromResult(MethodResult<List<Job>>.Success(jobsWithStatus));
        }
        catch (Exception ex)
        {
            return Task.FromResult(MethodResult<List<Job>>.Failure(
                AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error retrieving jobs by status: {ex.Message}", ex)));
        }
    }

    public Task<MethodResult> Update(Job job, CancellationToken cancellationToken)
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

            jobs[job.Id] = job;

            return Task.FromResult(MethodResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(MethodResult.Failure(
                AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error updating job: {ex.Message}", ex)));
        }
    }
}
