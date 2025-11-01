using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AsyncEndpoints.JobProcessing;

namespace AsyncEndpoints.Background;

/// <summary>
/// Defines a contract for claiming jobs from the job store and enqueuing them for processing
/// </summary>
public interface IJobClaimingService
{
	/// <summary>
	/// Claims and attempts to enqueue a job, returning the appropriate delay state based on the outcome
	/// </summary>
	/// <param name="writerJobChannel">The channel to write jobs to</param>
	/// <param name="workerId">The ID of the current worker</param>
	/// <param name="stoppingToken">Cancellation token</param>
	/// <returns>The result containing delay calculation state</returns>
	Task<JobClaimingState> ClaimAndEnqueueJobAsync(ChannelWriter<Job> writerJobChannel, Guid workerId, CancellationToken stoppingToken);
}
