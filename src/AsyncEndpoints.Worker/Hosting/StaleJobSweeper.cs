using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.Core.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.Worker.Hosting;

public sealed class StaleJobSweeper : BackgroundService
{
	private readonly IJobStore _store;
	private readonly AsyncEndpointsOptions _options;
	private readonly ILogger<StaleJobSweeper> _logger;

	public StaleJobSweeper(
		IJobStore store,
		IOptions<AsyncEndpointsOptions> options,
		ILogger<StaleJobSweeper> logger)
	{
		_store = store;
		_options = options.Value;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("StaleJobSweeper started");

		var interval = TimeSpan.FromMilliseconds(_options.StaleJobTimeout.TotalMilliseconds / 2);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(interval, stoppingToken);

				var reclaimed = await _store.ReclaimStaleJobsAsync(_options.StaleJobTimeout, stoppingToken);

				if (reclaimed > 0)
				{
					_logger.LogInformation("Reclaimed {Count} stale jobs", reclaimed);
				}
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error reclaiming stale jobs");
			}
		}

		_logger.LogInformation("StaleJobSweeper stopped");
	}
}
