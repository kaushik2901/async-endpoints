using AsyncEndpoints.Abstractions.Listener;
using AsyncEndpoints.Worker.Concurrency;
using AsyncEndpoints.Worker.Execution;
using AsyncEndpoints.Worker.Heartbeat;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.Worker.Hosting;

public sealed class JobWorkerService : BackgroundService
{
	private readonly string _channel;
	private readonly IJobListener _listener;
	private readonly JobExecutionPipeline _pipeline;
	private readonly WorkerConcurrencyManager _concurrencyManager;
	private readonly HeartbeatService _heartbeatService;
	private readonly ILogger<JobWorkerService> _logger;

	public JobWorkerService(
		string channel,
		IJobListener listener,
		JobExecutionPipeline pipeline,
		WorkerConcurrencyManager concurrencyManager,
		HeartbeatService heartbeatService,
		ILogger<JobWorkerService> logger)
	{
		_channel = channel;
		_listener = listener;
		_pipeline = pipeline;
		_concurrencyManager = concurrencyManager;
		_heartbeatService = heartbeatService;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("JobWorkerService started for channel {Channel}", _channel);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				var job = await _listener.WaitForNextJobAsync(_channel, null, stoppingToken);

				if (job is null)
				{
					continue;
				}

				_logger.LogDebug("Dequeued job {JobId} ({JobName}) on channel {Channel}",
					job.JobId, job.JobName, _channel);

				await _concurrencyManager.WaitAsync(_channel, job.Partition, stoppingToken);

				try
				{
					await using var heartbeat = await _heartbeatService.StartHeartbeat(job.JobId, stoppingToken);

					await _pipeline.ExecuteAsync(job, stoppingToken);
				}
				finally
				{
					_concurrencyManager.Release(_channel, job.Partition);
				}
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Unexpected error in worker loop for channel {Channel}", _channel);
			}
		}

		_logger.LogInformation("JobWorkerService stopped for channel {Channel}", _channel);
	}
}
