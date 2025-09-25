using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Utilities;

namespace AsyncEndpoints.Contracts;

/// <summary>
/// Defines a contract for storing and managing asynchronous jobs.
/// Provides methods for creating, retrieving, updating, and querying jobs.
/// </summary>
public interface IJobStore
{
    /// <summary>
    /// Retrieves a job by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the job.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="MethodResult{Job}"/> containing the job if found, or null if not found.</returns>
    Task<MethodResult<Job?>> Get(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new job to the store.
    /// </summary>
    /// <param name="job">The job to add.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="MethodResult"/> indicating the success or failure of the operation.</returns>
    Task<MethodResult> Add(Job job, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves a list of queued jobs for a specific worker.
    /// </summary>
    /// <param name="workerId">The unique identifier of the worker.</param>
    /// <param name="maxSize">The maximum number of jobs to retrieve.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="MethodResult{List{Job}}"/> containing the list of queued jobs.</returns>
    Task<MethodResult<List<Job>>> GetQueuedJobs(Guid workerId, int maxSize, CancellationToken cancellationToken);

    /// <summary>
    /// Updates the status of a job.
    /// </summary>
    /// <param name="id">The unique identifier of the job.</param>
    /// <param name="status">The new status for the job.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="MethodResult"/> indicating the success or failure of the operation.</returns>
    Task<MethodResult> UpdateJobStatus(Guid id, JobStatus status, CancellationToken cancellationToken);

    /// <summary>
    /// Updates the result of a job.
    /// </summary>
    /// <param name="id">The unique identifier of the job.</param>
    /// <param name="result">The result string to store.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="MethodResult"/> indicating the success or failure of the operation.</returns>
    Task<MethodResult> UpdateJobResult(Guid id, string result, CancellationToken cancellationToken);

    /// <summary>
    /// Updates the exception information for a job that failed.
    /// </summary>
    /// <param name="id">The unique identifier of the job.</param>
    /// <param name="exception">The exception string to store.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="MethodResult"/> indicating the success or failure of the operation.</returns>
    Task<MethodResult> UpdateJobException(Guid id, string exception, CancellationToken cancellationToken);
}
