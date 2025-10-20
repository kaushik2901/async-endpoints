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
		using var _ = _logger.BeginScope(new { WorkerId = workerId });
		
		_logger.LogDebug("Attempting to claim job for worker {WorkerId}", workerId);
		
		var claimedJobResult = await _jobManager.ClaimNextAvailableJob(workerId, stoppingToken);
		if (claimedJobResult.IsFailure)
		{
			_logger.LogError("Failed to claim job for processing: {Error}", claimedJobResult.Error?.Message);
			return JobClaimingState.ErrorOccurred;
		}

		var job = claimedJobResult.DataOrNull;
		if (job == null)
		{
			_logger.LogDebug("No job available for worker {WorkerId}", workerId);
			return JobClaimingState.NoJobFound;
		}

		_logger.LogDebug("Successfully claimed job {JobId}, attempting to enqueue", job.Id);
		
		var enqueued = await _jobChannelEnqueuer.Enqueue(writerJobChannel, job, stoppingToken);
		if (!enqueued)
		{
			_logger.LogError("Failed to enqueue job for processing: {JobId}", job.Id);
			return JobClaimingState.FailedToEnqueue;
		}

		_logger.LogDebug("Successfully enqueued job {JobId} for worker {WorkerId}", job.Id, workerId);
		return JobClaimingState.JobSuccessfullyEnqueued;
	}
}
