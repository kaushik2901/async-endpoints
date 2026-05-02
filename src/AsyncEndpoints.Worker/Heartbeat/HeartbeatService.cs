using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.Worker.Concurrency;
using Microsoft.Extensions.Hosting;

namespace AsyncEndpoints.Worker.Heartbeat;

/// <summary>
/// Background service that periodically heartbeats active jobs.
/// </summary>
public class HeartbeatService : BackgroundService
{
	private readonly IJobStore _store;
	private readonly WorkerConcurrencyManager _concurrencyManager;
	private readonly string _workerId;
	private readonly TimeSpan _interval;

	public HeartbeatService(
		IJobStore store,
		WorkerConcurrencyManager concurrencyManager,
		string workerId,
		TimeSpan interval)
	{
		_store = store;
		_concurrencyManager = concurrencyManager;
		_workerId = workerId;
		_interval = interval;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(_interval, stoppingToken);

				var activeJobs = _concurrencyManager.GetActiveJobIds().ToList();
				if (!activeJobs.Any()) continue;

				foreach (var jobId in activeJobs)
				{
					try
					{
						await _store.HeartbeatAsync(jobId, _workerId, stoppingToken);
					}
					catch
					{
						// Ignore heartbeat failures for specific jobs 
						// (e.g. if the job just finished or store is busy)
					}
				}
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch
			{
				// General error in heartbeat loop - should not stop the service
			}
		}
	}
}
