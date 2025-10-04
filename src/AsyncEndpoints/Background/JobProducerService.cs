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
public class JobProducerService(
	ILogger<JobProducerService> logger,
	IOptions<AsyncEndpointsConfigurations> configurations,
	IDelayCalculatorService delayCalculatorService,
	IServiceScopeFactory serviceScopeFactory) : IJobProducerService
{
	private readonly ILogger<JobProducerService> _logger = logger;
	private readonly AsyncEndpointsWorkerConfigurations _workerConfigurations = configurations.Value.WorkerConfigurations;
	private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
	private readonly IDelayCalculatorService _delayCalculatorService = delayCalculatorService;

	/// <summary>
	/// Produces jobs and writes them to the provided channel asynchronously.
	/// </summary>
	/// <param name="writerJobChannel">The channel writer to write jobs to.</param>
	/// <param name="stoppingToken">A cancellation token to stop the production process.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public async Task ProduceJobsAsync(ChannelWriter<Job> writerJobChannel, CancellationToken stoppingToken)
	{
		try
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				await using var scope = _serviceScopeFactory.CreateAsyncScope();
				var jobClaimingService = scope.ServiceProvider.GetRequiredService<IJobClaimingService>();

				try
				{
					var result = await jobClaimingService.ClaimAndEnqueueJobAsync(writerJobChannel, _workerConfigurations.WorkerId, stoppingToken);

					var delay = _delayCalculatorService.CalculateDelay(result, _workerConfigurations);

					await Task.Delay(delay, stoppingToken);
				}
				catch (OperationCanceledException)
				{
					break;
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error in job producer");
					var delay = _delayCalculatorService.CalculateDelay(JobClaimingState.ErrorOccurred, _workerConfigurations);
					await Task.Delay(delay, stoppingToken);
				}
			}
		}
		finally
		{
			writerJobChannel.Complete();
		}
	}
}
