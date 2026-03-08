using System.Threading;
using System.Threading.Tasks;

namespace AsyncEndpoints.JobProcessing;

public interface IJobRecoveryService
{
	/// <summary>
	/// Determines if this job store implementation supports job recovery
	/// </summary>
	bool SupportsJobRecovery { get; }

	/// <summary>
	/// Recovers stuck jobs that were in progress beyond the specified timeout
	/// </summary>
	/// <param name="timeoutUnixTime">The Unix timestamp before which jobs should be considered stuck</param>
	/// <param name="maxRetries">The maximum number of retries for failed jobs</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>The number of jobs recovered</returns>
	Task<int> RecoverStuckJobs(long timeoutUnixTime, int maxRetries, CancellationToken cancellationToken);
}
