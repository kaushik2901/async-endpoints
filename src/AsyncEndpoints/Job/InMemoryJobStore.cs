using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncEndpoints.Job;

public class InMemoryJobStore : IJobStore
{
    private readonly ConcurrentDictionary<Guid, Job> jobs = new();

    public Task Add(Job job, CancellationToken cancellationToken)
    {
        jobs.TryAdd(job.Id, job);
        return Task.CompletedTask;
    }

    public Task<Job?> Get(Guid id, CancellationToken cancellationToken)
    {
        jobs.TryGetValue(id, out var job);
        return Task.FromResult(job);
    }
}
