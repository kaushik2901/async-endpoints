using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Utilities;

namespace AsyncEndpoints.Contracts;

public interface IJobStore
{
    Task<MethodResult<Job?>> Get(Guid id, CancellationToken cancellationToken);
    Task<MethodResult> Add(Job job, CancellationToken cancellationToken);
}
