using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Entities;

namespace AsyncEndpoints.Services;

/// <summary>
/// Defines a contract for processing individual jobs.
/// </summary>
public interface IJobProcessorService
{
    /// <summary>
    /// Processes a single job asynchronously.
    /// </summary>
    /// <param name="job">The job to process.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ProcessAsync(Job job, CancellationToken cancellationToken);
}
