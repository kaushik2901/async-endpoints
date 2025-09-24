using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AsyncEndpoints.Entities;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.Services;

public class JobConsumerService(ILogger<JobConsumerService> logger, IJobProcessorService jobProcessorService) : IJobConsumerService
{
    private readonly ILogger<JobConsumerService> _logger = logger;
    private readonly IJobProcessorService _jobProcessorService = jobProcessorService;

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
                    await _jobProcessorService.ProcessAsync(job, stoppingToken);
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
            throw; // Let the background service handle the failure
        }
    }
}
