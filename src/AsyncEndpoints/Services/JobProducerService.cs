using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AsyncEndpoints.Contracts;
using AsyncEndpoints.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.Services;

public class JobProducerService(ILogger<JobProducerService> logger, IJobStore jobStore, IOptions<AsyncEndpointsConfigurations> configurations) : IJobProducerService
{
    private readonly ILogger<JobProducerService> _logger = logger;
    private readonly IJobStore _jobStore = jobStore;
    private readonly AsyncEndpointsWorkerConfigurations _workerConfigurations = configurations.Value.WorkerConfigurations;

    public async Task ProduceJobsAsync(ChannelWriter<Job> _writerJobChannel, CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var queuedJobsResult = await _jobStore.GetByStatus(JobStatus.Queued, _workerConfigurations.BatchSize, stoppingToken);
                    if (queuedJobsResult.IsFailure)
                    {
                        _logger.LogError("Failed to retrieve queued jobs: {Error}", queuedJobsResult.Error?.Message);
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                        continue;
                    }

                    var queuedJobs = queuedJobsResult.Data ?? [];
                    _logger.LogDebug("Found {Count} queued jobs to process", queuedJobs.Count);

                    foreach (var job in queuedJobs)
                    {
                        if (stoppingToken.IsCancellationRequested)
                            break;

                        await _writerJobChannel.WriteAsync(job, stoppingToken);
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(_workerConfigurations.PollingIntervalMs), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in job producer");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
        finally
        {
            _writerJobChannel.Complete();
        }
    }
}
