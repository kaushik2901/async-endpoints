using AsyncEndpoints.Abstractions.Listener;
using AsyncEndpoints.Worker.Concurrency;
using AsyncEndpoints.Worker.Execution;
using Microsoft.Extensions.Hosting;

namespace AsyncEndpoints.Worker.Hosting;

/// <summary>
/// Background service that runs the main worker loop.
/// </summary>
public class JobWorkerService : BackgroundService
{
	private readonly IJobListener _listener;
	private readonly JobExecutionPipeline _pipeline;
	private readonly WorkerConcurrencyManager _concurrencyManager;
	private readonly string _channel;

	public JobWorkerService(
		IJobListener listener,
		JobExecutionPipeline pipeline,
		WorkerConcurrencyManager concurrencyManager,
		string channel = "default")
	{
		_listener = listener;
		_pipeline = pipeline;
		_concurrencyManager = concurrencyManager;
		_channel = channel;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			// 1. Wait for available concurrency slot
			await _concurrencyManager.WaitAsync(stoppingToken);

			try
			{
				// 2. Wait for the next job from the listener
				// The listener should block or poll with a timeout.
				var job = await _listener.WaitForNextJobAsync(_channel, stoppingToken);

				if (job != null)
				{
					// 3. Hand off the job to the pipeline in a background task
					// The pipeline is responsible for registering the job in the manager
					// and releasing the slot when finished.
					_ = Task.Run(() => _pipeline.RunAsync(job, stoppingToken), stoppingToken);
				}
				else
				{
					// No job found, release the slot immediately
					_concurrencyManager.ReleaseSlot();
				}
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				// Shutdown requested, release slot if we were waiting
				_concurrencyManager.ReleaseSlot();
				break;
			}
			catch (Exception)
			{
				// Error in worker loop, release slot and wait a bit before retrying
				_concurrencyManager.ReleaseSlot();
				await Task.Delay(1000, stoppingToken);
			}
		}
	}
}
