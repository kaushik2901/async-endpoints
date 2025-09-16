using System;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncEndpoints.Job;

public interface IJobStore
{
    Task<Job?> Get(Guid id, CancellationToken cancellationToken);
    Task Add(Job job, CancellationToken cancellationToken);
}
