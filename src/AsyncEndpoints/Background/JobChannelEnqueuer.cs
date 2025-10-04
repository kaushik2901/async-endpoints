using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AsyncEndpoints.Configuration;
using AsyncEndpoints.JobProcessing;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.Background;

/// <summary>
/// Implements the IJobChannelEnqueuer interface to handle enqueuing jobs to a channel writer
/// </summary>
public class JobChannelEnqueuer(ILogger<JobChannelEnqueuer> logger) : IJobChannelEnqueuer
{
	private readonly ILogger<JobChannelEnqueuer> _logger = logger;

	/// <summary>
	/// Attempts to enqueue a job to the provided channel writer with both non-blocking and timeout-based approaches
	/// </summary>
	/// <param name="writerJobChannel">The channel writer to write the job to</param>
	/// <param name="job">The job to enqueue</param>
	/// <param name="stoppingToken">Cancellation token</param>
	/// <returns>True if successfully enqueued, false otherwise</returns>
	public async Task<bool> Enqueue(ChannelWriter<Job> writerJobChannel, Job job, CancellationToken stoppingToken)
	{
		// Try non-blocking write first
		if (writerJobChannel.TryWrite(job))
		{
			return true;
		}

		// Channel is full - use timeout to avoid indefinite blocking
		using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(AsyncEndpointsConstants.JobProducerChannelWriteTimeoutSeconds));
		using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeoutCts.Token);

		try
		{
			await writerJobChannel.WriteAsync(job, combinedCts.Token);

			return true;
		}
		catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
		{
			_logger.LogDebug("Channel write timeout - channel likely full");
			// Don't break here since we're only processing one job
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			_logger.LogDebug("Job producer was cancelled while writing to channel");
		}
		catch (ObjectDisposedException)
		{
			_logger.LogWarning("Channel was disposed while trying to write job {JobId}", job.Id);
			// Channel was disposed, likely service shutting down
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error writing job {JobId} to channel", job.Id);
		}

		return false;
	}
}
