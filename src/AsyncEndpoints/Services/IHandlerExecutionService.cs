using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Utilities;

namespace AsyncEndpoints.Services;

/// <summary>
/// Defines a contract for executing handlers associated with specific jobs.
/// </summary>
public interface IHandlerExecutionService
{
	/// <summary>
	/// Executes the handler associated with the specified job name.
	/// </summary>
	/// <param name="jobName">The unique name of the job, used to identify the specific handler.</param>
	/// <param name="request">The request object to pass to the handler.</param>
	/// <param name="job">The job entity containing additional context.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A <see cref="MethodResult{object}"/> containing the result of the handler execution.</returns>
	Task<MethodResult<object>> ExecuteHandlerAsync(string jobName, object request, Job job, CancellationToken cancellationToken);
}