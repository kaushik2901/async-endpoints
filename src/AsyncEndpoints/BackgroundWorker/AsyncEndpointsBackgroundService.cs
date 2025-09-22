using System;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AsyncEndpoints.Contracts;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.BackgroundWorker;

public class AsyncEndpointsBackgroundService : BackgroundService, IDisposable
{
    private readonly ILogger<AsyncEndpointsBackgroundService> _logger;
    private readonly IJobStore _jobStore;
    private readonly AsyncEndpointsWorkerConfigurations _workerConfigurations;
    private readonly Channel<Job> _jobChannel;
    private readonly ChannelReader<Job> _readerJobChannel;
    private readonly ChannelWriter<Job> _writerJobChannel;
    private readonly SemaphoreSlim _semaphoreSlim;

    private bool _disposed = false;

    public AsyncEndpointsBackgroundService(
        ILogger<AsyncEndpointsBackgroundService> logger,
        IOptions<AsyncEndpointsConfigurations> configurations,
        IServiceProvider serviceProvider,
        IJobStore jobStore)
    {
        _logger = logger;
        _workerConfigurations = configurations.Value.WorkerConfigurations;
        _jobStore = jobStore;

        var channelOptions = new BoundedChannelOptions(_workerConfigurations.MaximumQueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };

        _jobChannel = Channel.CreateBounded<Job>(channelOptions);
        _readerJobChannel = _jobChannel.Reader;
        _writerJobChannel = _jobChannel.Writer;
        _semaphoreSlim = new SemaphoreSlim(
            _workerConfigurations.MaximumConcurrency,
            _workerConfigurations.MaximumConcurrency
        );
    }

    public override void Dispose()
    {
        base.Dispose();
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _semaphoreSlim.Dispose();
            _writerJobChannel.Complete();
            _disposed = true;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AsyncEndpoints Background Service is starting");

        var producerTask = ProduceJobsAsync(stoppingToken);
        var consumerTasks = Enumerable.Range(0, _workerConfigurations.MaximumConcurrency)
            .Select(_ => ConsumeJobsAsync(stoppingToken))
            .ToArray();

        await Task.WhenAll([producerTask, .. consumerTasks]);

        _logger.LogInformation("AsyncEndpoints Background Service is stopping");
    }

    private async Task ProduceJobsAsync(CancellationToken stoppingToken)
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

    private async Task ConsumeJobsAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _readerJobChannel.ReadAllAsync(stoppingToken))
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            await _semaphoreSlim.WaitAsync(stoppingToken);

            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessJobAsync(job, stoppingToken);
                }
                finally
                {
                    _semaphoreSlim.Release();
                }
            }, stoppingToken);
        }
    }

    private async Task ProcessJobAsync(Job job, CancellationToken cancellationToken)
    {
        job.UpdateStatus(JobStatus.InProgress);

        var updateResult = await _jobStore.Update(job, cancellationToken);
        if (updateResult.IsFailure)
        {
            _logger.LogError("Failed to update job {JobId} status to InProgress: {Error}", job.Id, updateResult.Error?.Message);
            return;
        }

        try
        {
            var result = await ProcessJobPayloadAsync(job, cancellationToken);

            if (result.IsSuccess)
            {
                job.UpdateStatus(JobStatus.Completed);
                job.SetResult(result.Data?.ToString() ?? string.Empty);
            }
            else
            {
                job.UpdateStatus(JobStatus.Failed);
                job.SetException(result.Error?.ToString() ?? "Unknown error");
            }

            await _jobStore.Update(job, cancellationToken);

            if (result.IsSuccess)
                _logger.LogInformation("Successfully processed job {JobId}", job.Id);
            else
                _logger.LogError("Failed to process job {JobId}: {Error}", job.Id, result.Error?.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing job {JobId}", job.Id);

            if (job.RetryCount < job.MaxRetries)
            {
                job.IncrementRetryCount();
                job.UpdateStatus(JobStatus.Queued);
                job.SetException(ex.Message);
                await _jobStore.Update(job, cancellationToken);

                _logger.LogInformation("Job {JobId} will be retried. Retry count: {RetryCount}", job.Id, job.RetryCount);
            }
            else
            {
                // Mark as failed after max retries
                job.UpdateStatus(JobStatus.Failed);
                job.SetException(ex.Message);
                await _jobStore.Update(job, cancellationToken);

                _logger.LogError("Job {JobId} failed after {RetryCount} retries", job.Id, job.RetryCount);
            }
        }
    }

    private async Task<MethodResult<object?>> ProcessJobPayloadAsync(Job job, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing job {JobId} with name {JobName}", job.Id, job.Name);

        // In a real implementation, we would:
        // 1. Deserialize the payload based on the job name
        // 2. Resolve the appropriate handler from the service provider
        // 3. Call the handler with the deserialized payload
        // 4. Store the result in the job

        // For now, we'll simulate the process
        try
        {
            // Simulate some work
            await Task.Delay(1000, cancellationToken);

            // Simulate a successful result
            return MethodResult<object?>.Success(new { Message = "Job processed successfully", JobId = job.Id });
        }
        catch (Exception ex)
        {
            return MethodResult<object?>.Failure(ex);
        }
    }
}
