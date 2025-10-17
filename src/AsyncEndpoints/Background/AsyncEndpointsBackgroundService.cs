using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AsyncEndpoints.Configuration;
using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.JobProcessing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.Background;

/// <summary>
/// A background service that manages the processing of asynchronous jobs using producer-consumer pattern.
/// It coordinates job production and consumption with configurable concurrency and queue limits.
/// </summary>
public sealed class AsyncEndpointsBackgroundService : BackgroundService, IAsyncDisposable, IDisposable
{
	private readonly ILogger<AsyncEndpointsBackgroundService> _logger;
	private readonly IJobProducerService _jobProducerService;
	private readonly IJobConsumerService _jobConsumerService;
	private readonly AsyncEndpointsWorkerConfigurations _workerConfigurations;
	private readonly ChannelReader<Job> _readerJobChannel;
	private readonly ChannelWriter<Job> _writerJobChannel;
	private readonly SemaphoreSlim _semaphoreSlim;
	private readonly CancellationTokenSource _shutdownTokenSource = new();
	private readonly SemaphoreSlim _shutdownSemaphore = new(1, 1);
	private readonly IDateTimeProvider DateTimeProvider;
	private bool _disposed = false;

	public AsyncEndpointsBackgroundService(
		ILogger<AsyncEndpointsBackgroundService> logger,
		IOptions<AsyncEndpointsConfigurations> configurations,
		IJobProducerService jobProducerService,
		IJobConsumerService jobConsumerService,
		IDateTimeProvider dateTimeProvider)
	{
		_logger = logger;
		_workerConfigurations = configurations.Value.WorkerConfigurations;
		_jobProducerService = jobProducerService;
		_jobConsumerService = jobConsumerService;
		DateTimeProvider = dateTimeProvider;

		var channelOptions = new BoundedChannelOptions(_workerConfigurations.MaximumQueueSize)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = false,
			SingleWriter = false
		};

		var jobChannel = Channel.CreateBounded<Job>(channelOptions);
		_readerJobChannel = jobChannel.Reader;
		_writerJobChannel = jobChannel.Writer;
		_semaphoreSlim = new SemaphoreSlim(
			_workerConfigurations.MaximumConcurrency,
			_workerConfigurations.MaximumConcurrency
		);
	}

	/// <summary>
	/// Disposes of the resources used by the background service.
	/// </summary>
	public override void Dispose()
	{
		Dispose(true);
		base.Dispose();
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Asynchronously disposes of the resources used by the background service.
	/// </summary>
	/// <returns>A task representing the asynchronous disposal operation.</returns>
	public async ValueTask DisposeAsync()
	{
		await DisposeAsyncCore().ConfigureAwait(false);
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Disposes of the resources used by the background service.
	/// </summary>
	/// <param name="disposing">True if disposing resources, false if finalizing.</param>
	public void Dispose(bool disposing)
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

	/// <summary>
	/// Asynchronously disposes of the resources used by the background service.
	/// </summary>
	/// <returns>A task representing the asynchronous disposal operation.</returns>
	public async ValueTask DisposeAsyncCore()
	{
		if (_disposed)
			return;

		await _shutdownSemaphore.WaitAsync().ConfigureAwait(false);
		try
		{
			if (_disposed)
				return;

			_logger.LogInformation("Starting graceful shutdown of AsyncEndpoints Background Service");

			await _shutdownTokenSource.CancelAsync();

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

	/// <summary>
	/// Executes the background service asynchronously.
	/// </summary>
	/// <param name="stoppingToken">A cancellation token that signals when the service should stop.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("AsyncEndpoints Background Service is starting");

		var producerTask = _jobProducerService.ProduceJobsAsync(_writerJobChannel, stoppingToken);
		var consumerTasks = Enumerable.Range(0, _workerConfigurations.MaximumConcurrency)
			.Select(_ => _jobConsumerService.ConsumeJobsAsync(_readerJobChannel, _semaphoreSlim, stoppingToken))
			.ToArray();

		try
		{
			List<Task> tasks = [producerTask, .. consumerTasks];
			await Task.WhenAll(tasks);
		}
		catch (Exception ex)
		{
			// Log which components failed
			if (producerTask.IsFaulted)
			{
				_logger.LogError(producerTask.Exception, "Job producer task failed");
			}

			for (int i = 0; i < consumerTasks.Length; i++)
			{
				if (consumerTasks[i].IsFaulted)
				{
					_logger.LogError(consumerTasks[i].Exception, "Job consumer task {Index} failed", i);
				}
			}

			_logger.LogError(ex, "AsyncEndpoints Background Service encountered an unrecoverable error");
		}

		_logger.LogInformation("AsyncEndpoints Background Service is stopping");
	}

	/// <summary>
	/// Waits for all processing work to complete during shutdown.
	/// </summary>
	/// <param name="timeout">The maximum time to wait for work to complete.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	private async Task WaitForWorkCompletionAsync(TimeSpan timeout)
	{
		var deadline = DateTimeProvider.UtcNow.Add(timeout);

		// Wait for semaphore to indicate all work is done
		// (All permits available means no work in progress)
		while (DateTimeProvider.UtcNow < deadline)
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
