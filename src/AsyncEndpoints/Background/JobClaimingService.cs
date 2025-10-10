using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AsyncEndpoints.JobProcessing;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.Background;

/// <inheritdoc />
public class JobClaimingService(ILogger<JobClaimingService> logger, IJobManager jobManager, IJobChannelEnqueuer jobChannelEnqueuer) : IJobClaimingService
{
	private readonly ILogger<JobClaimingService> _logger = logger;
	private readonly IJobManager _jobManager = jobManager;
	private readonly IJobChannelEnqueuer _jobChannelEnqueuer = jobChannelEnqueuer;

	/// <inheritdoc />
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
