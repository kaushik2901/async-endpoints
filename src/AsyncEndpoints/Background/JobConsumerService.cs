using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AsyncEndpoints.JobProcessing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.Background;

/// <inheritdoc />
public class JobConsumerService(ILogger<JobConsumerService> logger, IServiceScopeFactory serviceScopeFactory) : IJobConsumerService
{
	private readonly ILogger<JobConsumerService> _logger = logger;
	private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;

	/// <inheritdoc />
	public async Task ConsumeJobsAsync(ChannelReader<Job> readerJobChannel, SemaphoreSlim semaphoreSlim, CancellationToken stoppingToken)
	{
		_logger.LogDebug("Starting job consumption loop");
		
		try
		{
			await foreach (var job in readerJobChannel.ReadAllAsync(stoppingToken))
			{
				if (stoppingToken.IsCancellationRequested)
				{
					_logger.LogDebug("Cancellation requested, exiting job consumption loop");
					break;
				}

				_logger.LogDebug("Acquiring semaphore for job {JobId}", job.Id);
				await semaphoreSlim.WaitAsync(stoppingToken);

				try
				{
					_logger.LogDebug("Processing job {JobId} on consumer thread", job.Id);
					await using var scope = _serviceScopeFactory.CreateAsyncScope();
					var jobProcessorService = scope.ServiceProvider.GetRequiredService<IJobProcessorService>();
					await jobProcessorService.ProcessAsync(job, stoppingToken);
					_logger.LogDebug("Completed processing job {JobId}", job.Id);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error processing job {JobId}", job.Id);
				}
				finally
				{
					semaphoreSlim.Release();
					_logger.LogDebug("Released semaphore, available permits: {AvailablePermits}", semaphoreSlim.CurrentCount);
				}
			}
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			_logger.LogDebug("Job consumption loop cancelled");
		}

		_logger.LogDebug("Job consumption loop finished");
	}
}
