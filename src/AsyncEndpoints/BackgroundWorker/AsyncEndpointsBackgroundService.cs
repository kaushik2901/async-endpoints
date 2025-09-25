using System;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.BackgroundWorker;

/// <summary>
/// A background service that manages the processing of asynchronous jobs using producer-consumer pattern.
/// It coordinates job production and consumption with configurable concurrency and queue limits.
/// </summary>
public class AsyncEndpointsBackgroundService : BackgroundService, IAsyncDisposable, IDisposable
{
    private readonly ILogger<AsyncEndpointsBackgroundService> _logger;
    private readonly IJobProducerService _jobProducerService;
    private readonly IJobConsumerService _jobConsumerService;
    private readonly AsyncEndpointsWorkerConfigurations _workerConfigurations;
    private readonly Channel<Job> _jobChannel;
    private readonly ChannelReader<Job> _readerJobChannel;
    private readonly ChannelWriter<Job> _writerJobChannel;
    private readonly SemaphoreSlim _semaphoreSlim;
    private readonly CancellationTokenSource _shutdownTokenSource = new();
    private readonly SemaphoreSlim _shutdownSemaphore = new(1, 1);
    private bool _disposed = false;

    public AsyncEndpointsBackgroundService(
        ILogger<AsyncEndpointsBackgroundService> logger,
        IOptions<AsyncEndpointsConfigurations> configurations,
        IJobProducerService jobProducerService,
        IJobConsumerService jobConsumerService)
    {
        _logger = logger;
        _workerConfigurations = configurations.Value.WorkerConfigurations;
        _jobProducerService = jobProducerService;
        _jobConsumerService = jobConsumerService;

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
        Dispose(true);
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                _shutdownTokenSource.Cancel();
                _writerJobChannel.TryComplete();

                _semaphoreSlim.Dispose();
                _shutdownTokenSource.Dispose();
                _shutdownSemaphore.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during synchronous disposal");
            }
            finally
            {
                _disposed = true;
            }
        }
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_disposed)
            return;

        await _shutdownSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
                return;

            _logger.LogInformation("Starting graceful shutdown of AsyncEndpoints Background Service");

            _shutdownTokenSource.Cancel();

            _writerJobChannel.TryComplete();

            await WaitForWorkCompletionAsync(TimeSpan.FromSeconds(AsyncEndpointsConstants.BackgroundServiceShutdownTimeoutSeconds)).ConfigureAwait(false);

            _logger.LogInformation("AsyncEndpoints Background Service graceful shutdown completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during async disposal");
        }
        finally
        {
            _shutdownSemaphore.Release();
            _disposed = true;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AsyncEndpoints Background Service is starting");

        var producerTask = _jobProducerService.ProduceJobsAsync(_writerJobChannel, stoppingToken);
        var consumerTasks = Enumerable.Range(0, _workerConfigurations.MaximumConcurrency)
            .Select(_ => _jobConsumerService.ConsumeJobsAsync(_readerJobChannel, _semaphoreSlim, stoppingToken))
            .ToArray();

        await Task.WhenAll([producerTask, .. consumerTasks]);

        _logger.LogInformation("AsyncEndpoints Background Service is stopping");
    }

    private async Task WaitForWorkCompletionAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);

        // Wait for semaphore to indicate all work is done
        // (All permits available means no work in progress)
        while (DateTime.UtcNow < deadline)
        {
            if (_semaphoreSlim.CurrentCount == _workerConfigurations.MaximumConcurrency)
            {
                // All permits available, no work in progress
                break;
            }

            await Task.Delay(AsyncEndpointsConstants.BackgroundServiceWaitDelayMs).ConfigureAwait(false);
        }

        if (_semaphoreSlim.CurrentCount < _workerConfigurations.MaximumConcurrency)
        {
            _logger.LogWarning("Some work may still be in progress during shutdown. Active jobs: {ActiveJobs}",
                _workerConfigurations.MaximumConcurrency - _semaphoreSlim.CurrentCount);
        }
    }
}
