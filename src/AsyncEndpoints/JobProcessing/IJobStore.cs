using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Utilities;

namespace AsyncEndpoints.JobProcessing;

/// <summary>
/// Defines a contract for storing and managing asynchronous jobs.
/// Provides methods for creating, retrieving, updating, and querying jobs.
/// </summary>
public interface IJobStore
{
	/// <summary>
	/// Creates a new job in the store
	/// </summary>
	Task<MethodResult> CreateJob(Job job, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves a job by its unique identifier
	/// </summary>
	Task<MethodResult<Job>> GetJobById(Guid id, CancellationToken cancellationToken);

	/// <summary>
	/// Updates the complete job entity
	/// </summary>
	Task<MethodResult> UpdateJob(Job job, CancellationToken cancellationToken);

	/// <summary>
	/// Atomically claims the next available job for a specific worker
	/// </summary>
	Task<MethodResult<Job>> ClaimNextJobForWorker(Guid workerId, CancellationToken cancellationToken);

	/// <summary>
	/// Determines if this job store implementation supports job recovery
	/// </summary>
	bool SupportsJobRecovery { get; }

	/// <summary>
	/// Recovers stuck jobs that were in progress beyond the specified timeout
	/// </summary>
	/// <param name="timeoutUnixTime">The Unix timestamp before which jobs should be considered stuck</param>
	/// <param name="maxRetries">The maximum number of retries for failed jobs</param>
	/// <param name="retryDelayBaseSeconds">The base delay for exponential backoff</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>The number of jobs recovered</returns>
	Task<int> RecoverStuckJobs(long timeoutUnixTime, int maxRetries, double retryDelayBaseSeconds, CancellationToken cancellationToken);
}
