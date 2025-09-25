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
    Task<MethodResult<List<Job>>> GetQueuedJobs(int maxSize, CancellationToken cancellationToken);
    Task<MethodResult> UpdateJobStatus(Guid id, JobStatus status, CancellationToken cancellationToken);
    Task<MethodResult> UpdateJobResult(Guid id, string result, CancellationToken cancellationToken);
    Task<MethodResult> UpdateJobException(Guid id, string exception, CancellationToken cancellationToken);
}
