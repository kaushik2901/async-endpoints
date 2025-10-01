using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AsyncEndpoints.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.Services;

/// <summary>
/// Implements the IJobConsumerService interface to consume jobs from a channel and process them.
/// Uses a semaphore to control the level of concurrency for job processing.
/// </summary>
public class JobConsumerService(ILogger<JobConsumerService> logger, IServiceScopeFactory serviceScopeFactory) : IJobConsumerService
{
	private readonly ILogger<JobConsumerService> _logger = logger;
	private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;

	/// <summary>
	/// Consumes jobs from the provided channel and processes them asynchronously.
	/// </summary>
	/// <param name="readerJobChannel">The channel reader to read jobs from.</param>
	/// <param name="semaphoreSlim">The semaphore to control concurrency.</param>
	/// <param name="stoppingToken">A cancellation token to stop the consumption process.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public async Task ConsumeJobsAsync(ChannelReader<Job> readerJobChannel, SemaphoreSlim semaphoreSlim, CancellationToken stoppingToken)
	{
		try
		{
			await foreach (var job in readerJobChannel.ReadAllAsync(stoppingToken))
			{
				if (stoppingToken.IsCancellationRequested)
					break;

				await semaphoreSlim.WaitAsync(stoppingToken);

				try
				{
					await using var scope = _serviceScopeFactory.CreateAsyncScope();
					var jobProcessorService = scope.ServiceProvider.GetRequiredService<IJobProcessorService>();
					await jobProcessorService.ProcessAsync(job, stoppingToken);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error processing job {JobId}", job.Id);
				}
				finally
				{
					semaphoreSlim.Release();
				}
			}
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			// Expected during shutdown
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Consumer task failed");
			throw;
		}
	}
}
