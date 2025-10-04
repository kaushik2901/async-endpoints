using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Utilities;
using Microsoft.AspNetCore.Http;

namespace AsyncEndpoints.JobProcessing;

/// <summary>
/// Defines a contract for managing the job lifecycle, retries, worker assignment, and scheduling.
/// </summary>
public interface IJobManager
{
	/// <summary>
	/// Submits a new job to the system
	/// </summary>
	Task<MethodResult<Job>> SubmitJob(string jobName, string payload, HttpContext httpContext, CancellationToken cancellationToken);

	/// <summary>
	/// Claims the next available job for processing by a worker
	/// </summary>
	Task<MethodResult<Job>> ClaimNextAvailableJob(Guid workerId, CancellationToken cancellationToken);

	/// <summary>
	/// Processes a successful job completion
	/// </summary>
	Task<MethodResult> ProcessJobSuccess(Guid jobId, string result, CancellationToken cancellationToken);

	/// <summary>
	/// Processes a failed job (with potential retry logic)
	/// </summary>
	Task<MethodResult> ProcessJobFailure(Guid jobId, AsyncEndpointError error, CancellationToken cancellationToken);

	/// <summary>
	/// Gets a job by its ID
	/// </summary>
	Task<MethodResult<Job>> GetJobById(Guid jobId, CancellationToken cancellationToken);
}