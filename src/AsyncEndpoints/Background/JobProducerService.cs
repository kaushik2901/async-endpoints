using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AsyncEndpoints.Configuration;
using AsyncEndpoints.JobProcessing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.Background;

/// <summary>
/// Implements the IJobProducerService interface to produce jobs and write them to a channel.
/// Polls the job manager for queued jobs and writes them to the channel for consumption.
/// Implements adaptive polling based on job availability and channel capacity.
/// </summary>
public class JobProducerService(ILogger<JobProducerService> logger, IServiceScopeFactory serviceScopeFactory, IOptions<AsyncEndpointsConfigurations> configurations) : IJobProducerService
{
	private readonly ILogger<JobProducerService> _logger = logger;
	private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
	private readonly AsyncEndpointsWorkerConfigurations _workerConfigurations = configurations.Value.WorkerConfigurations;

	/// <summary>
	/// Produces jobs and writes them to the provided channel asynchronously.
	/// </summary>
	/// <param name="writerJobChannel">The channel writer to write jobs to.</param>
	/// <param name="stoppingToken">A cancellation token to stop the production process.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public async Task ProduceJobsAsync(ChannelWriter<Job> writerJobChannel, CancellationToken stoppingToken)
	{
		var basePollingInterval = TimeSpan.FromMilliseconds(_workerConfigurations.PollingIntervalMs);

		try
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				var adaptiveDelay = basePollingInterval;

				try
				{
					await using var scope = _serviceScopeFactory.CreateAsyncScope();
					var jobManager = scope.ServiceProvider.GetRequiredService<IJobManager>();

					var queuedJobsResult = await jobManager.ClaimJobsForProcessing(_workerConfigurations.WorkerId, _workerConfigurations.BatchSize, stoppingToken);
					if (queuedJobsResult.IsFailure)
					{
						_logger.LogError("Failed to claim jobs for processing: {Error}", queuedJobsResult.Error?.Message);
						adaptiveDelay = TimeSpan.FromSeconds(AsyncEndpointsConstants.JobProducerErrorDelaySeconds);
						await Task.Delay(adaptiveDelay, stoppingToken);
						continue;
					}

					var queuedJobs = queuedJobsResult.Data ?? [];

					_logger.LogDebug("Found {Count} jobs to process", queuedJobs.Count);

					if (queuedJobs.Count == 0)
					{
						// No jobs found - use longer delay
						adaptiveDelay = TimeSpan.FromMilliseconds(Math.Min(_workerConfigurations.PollingIntervalMs * 3, AsyncEndpointsConstants.JobProducerMaxDelayMs));
					}
					else
					{
						var enqueuedCount = 0;

						foreach (var job in queuedJobs)
						{
							if (stoppingToken.IsCancellationRequested)
								break;

							// Try non-blocking write first
							if (writerJobChannel.TryWrite(job))
							{
								enqueuedCount++;
							}
							else
							{
								// Channel is full - use timeout to avoid indefinite blocking
								using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(AsyncEndpointsConstants.JobProducerChannelWriteTimeoutSeconds));
								using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeoutCts.Token);

								try
								{
									await writerJobChannel.WriteAsync(job, combinedCts.Token);
									enqueuedCount++;
								}
								catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
								{
									_logger.LogDebug("Channel write timeout - channel likely full");
									break;
								}
								catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
								{
									_logger.LogDebug("Job producer was cancelled while writing to channel");
									break;
								}
								catch (ObjectDisposedException)
								{
									_logger.LogWarning("Channel was disposed while trying to write job {JobId}", job.Id);
									break; // Channel was disposed, likely service shutting down
								}
								catch (Exception ex)
								{
									_logger.LogError(ex, "Unexpected error writing job {JobId} to channel", job.Id);
									// Consider whether to continue or break based on error type
									break; // Break to prevent continuous errors
								}
							}
						}

						// Adaptive delay based on enqueueing success
						adaptiveDelay = enqueuedCount == queuedJobs.Count
							? basePollingInterval
							: TimeSpan.FromMilliseconds(_workerConfigurations.PollingIntervalMs * 2);
					}

					await Task.Delay(adaptiveDelay, stoppingToken);
				}
				catch (OperationCanceledException)
				{
					break;
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error in job producer");
					adaptiveDelay = TimeSpan.FromSeconds(AsyncEndpointsConstants.JobProducerErrorDelaySeconds);
					await Task.Delay(adaptiveDelay, stoppingToken);
				}
			}
		}
		finally
		{
			writerJobChannel.Complete();
		}
	}
}
