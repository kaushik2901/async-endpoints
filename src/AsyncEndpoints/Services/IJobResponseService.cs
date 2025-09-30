using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace AsyncEndpoints.Services;

/// <summary>
/// Defines a contract for retrieving job responses by job ID.
/// </summary>
public interface IJobResponseService
{
    /// <summary>
    /// Retrieves a job response by its ID.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>An <see cref="IResult"/> representing the HTTP response containing the job information.</returns>
    Task<IResult> GetJobResponseAsync(Guid jobId, CancellationToken cancellationToken);
}