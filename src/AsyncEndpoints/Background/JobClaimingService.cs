using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AsyncEndpoints.JobProcessing;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.Background;

/// <summary>
/// Implements the IJobClaimingService interface to handle claiming jobs from the job manager and enqueuing them to the processing channel
/// </summary>
public class JobClaimingService(ILogger<JobClaimingService> logger, IJobManager jobManager, IJobChannelEnqueuer jobChannelEnqueuer) : IJobClaimingService
{
	private readonly ILogger<JobClaimingService> _logger = logger;
	private readonly IJobManager _jobManager = jobManager;
	private readonly IJobChannelEnqueuer _jobChannelEnqueuer = jobChannelEnqueuer;

	/// <summary>
	/// Claims the next available job and attempts to enqueue it, returning the appropriate delay state based on the outcome
	/// </summary>
	/// <param name="writerJobChannel">The channel to write jobs to</param>
	/// <param name="workerId">The ID of the current worker</param>
	/// <param name="stoppingToken">Cancellation token</param>
	/// <returns>The result containing delay calculation state</returns>
	public async Task<JobClaimingState> ClaimAndEnqueueJobAsync(ChannelWriter<Job> writerJobChannel, Guid workerId, CancellationToken stoppingToken)
	{
		var claimedJobResult = await _jobManager.ClaimNextAvailableJob(workerId, stoppingToken);
		if (claimedJobResult.IsFailure)
		{
			_logger.LogError("Failed to claim job for processing: {Error}", claimedJobResult.Error?.Message);
			return JobClaimingState.ErrorOccurred;
		}

		var job = claimedJobResult.DataOrNull;
		if (job == null)
		{
			return JobClaimingState.NoJobFound;
		}

		var enqueued = await _jobChannelEnqueuer.Enqueue(writerJobChannel, job, stoppingToken);
		if (!enqueued)
		{
			_logger.LogError("Failed to enqueue job for processing: {jobId}", job.Id);
			return JobClaimingState.FailedToEnqueue;
		}

		return JobClaimingState.JobSuccessfullyEnqueued;
	}
}
