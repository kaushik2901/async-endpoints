using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AsyncEndpoints.Configuration;
using AsyncEndpoints.JobProcessing;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.Background;

/// <inheritdoc />
public class JobChannelEnqueuer(ILogger<JobChannelEnqueuer> logger) : IJobChannelEnqueuer
{
	private readonly ILogger<JobChannelEnqueuer> _logger = logger;

	/// <inheritdoc />
	public async Task<bool> Enqueue(ChannelWriter<Job> writerJobChannel, Job job, CancellationToken stoppingToken)
	{
		using var _ = _logger.BeginScope(new { JobId = job.Id });

		_logger.LogDebug("Attempting to enqueue job {JobId} to channel", job.Id);

		// Try non-blocking write first
		if (writerJobChannel.TryWrite(job))
		{
			_logger.LogDebug("Successfully enqueued job {JobId} using non-blocking write", job.Id);
			return true;
		}

		_logger.LogDebug("Channel is full, using async write with timeout for job {JobId}", job.Id);

		// Channel is full - use timeout to avoid indefinite blocking
		using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(AsyncEndpointsConstants.JobProducerChannelWriteTimeoutSeconds));
		using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeoutCts.Token);

		try
		{
			await writerJobChannel.WriteAsync(job, combinedCts.Token);

			_logger.LogDebug("Successfully enqueued job {JobId} using async write", job.Id);
			return true;
		}
		catch (OperationCanceledException ex) when (timeoutCts.Token.IsCancellationRequested)
		{
			_logger.LogDebug(ex, "Channel write timeout - channel likely full");
			// Don't break here since we're only processing one job
		}
		catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
		{
			_logger.LogDebug(ex, "Job producer was cancelled while writing to channel");
		}
		catch (ObjectDisposedException ex)
		{
			_logger.LogWarning(ex, "Channel was disposed while trying to write job {JobId}", job.Id);
			// Channel was disposed, likely service shutting down
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error writing job {JobId} to channel", job.Id);
		}

		_logger.LogDebug("Failed to enqueue job {JobId} to channel", job.Id);
		return false;
	}
}
