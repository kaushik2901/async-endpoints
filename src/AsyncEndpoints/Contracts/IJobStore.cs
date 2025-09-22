using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Utilities;

namespace AsyncEndpoints.Contracts;

public interface IJobStore
{
    Task<MethodResult<Job?>> Get(Guid id, CancellationToken cancellationToken);
    Task<MethodResult> Add(Job job, CancellationToken cancellationToken);
    Task<MethodResult<List<Job>>> GetByStatus(JobStatus status, int maxSize, CancellationToken cancellationToken);
    Task<MethodResult> Update(Job job, CancellationToken cancellationToken);
}
