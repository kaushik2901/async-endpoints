using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Utilities;

namespace AsyncEndpoints.Job;

public class InMemoryJobStore : IJobStore
{
    private readonly ConcurrentDictionary<Guid, Job> jobs = new();

    public Task<MethodResult> Add(Job job, CancellationToken cancellationToken)
    {
        try
        {
            jobs.TryAdd(job.Id, job);
            return Task.FromResult(MethodResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(MethodResult.Failure(ex));
        }
    }

    public Task<MethodResult<Job?>> Get(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            jobs.TryGetValue(id, out var job);
            return Task.FromResult(MethodResult<Job?>.Success(job));
        }
        catch (Exception ex)
        {
            return Task.FromResult(MethodResult<Job?>.Failure(ex));
        }
    }
}
