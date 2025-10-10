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
