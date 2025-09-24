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
                    var queuedJobsResult = await _jobStore.GetByStatus(JobStatus.Queued, _workerConfigurations.BatchSize, stoppingToken);

                    if (queuedJobsResult.IsFailure)
                    {
                        _logger.LogError("Failed to retrieve queued jobs: {Error}", queuedJobsResult.Error?.Message);
                        adaptiveDelay = TimeSpan.FromSeconds(5);
                        await Task.Delay(adaptiveDelay, stoppingToken);
                        continue;
                    }

                    var queuedJobs = queuedJobsResult.Data ?? [];
                    _logger.LogDebug("Found {Count} queued jobs to process", queuedJobs.Count);

                    if (queuedJobs.Count == 0)
                    {
                        // No jobs found - use longer delay
                        adaptiveDelay = TimeSpan.FromMilliseconds(Math.Min(_workerConfigurations.PollingIntervalMs * 3, 30000));
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
                                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
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
                    adaptiveDelay = TimeSpan.FromSeconds(5);
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
