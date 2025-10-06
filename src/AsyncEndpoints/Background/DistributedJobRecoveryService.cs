using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Configuration;
using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.JobProcessing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.Background
{
	/// <summary>
	/// Background service that recovers stuck jobs that were in progress during system failures.
	/// This service is enabled when AddAsyncEndpointsWorker is called with recovery configuration.
	/// </summary>
	public class DistributedJobRecoveryService(
		ILogger<DistributedJobRecoveryService> logger,
		IJobStore jobStore,
		IDateTimeProvider dateTimeProvider,
		AsyncEndpointsRecoveryConfiguration recoveryConfig) : BackgroundService
	{
		private readonly ILogger<DistributedJobRecoveryService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
		private readonly IJobStore _jobStore = jobStore ?? throw new ArgumentNullException(nameof(jobStore));
		private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
		private readonly TimeSpan _recoveryInterval = TimeSpan.FromSeconds(recoveryConfig.RecoveryCheckIntervalSeconds);
		private readonly int _jobTimeoutMinutes = recoveryConfig.JobTimeoutMinutes;
		private readonly int _maxRetries = recoveryConfig.MaximumRetries;
		private readonly double _retryDelayBaseSeconds = recoveryConfig.RetryDelayBaseSeconds;

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			if (!_jobStore.SupportsJobRecovery)
			{
				_logger.LogWarning("Job Recovery Service is enabled but current job store does not support recovery. Service will not start.");
				return;
			}

			_logger.LogInformation("Job Recovery Service starting with timeout {Timeout} minutes and check interval {Interval} seconds",
				_jobTimeoutMinutes, _recoveryInterval.TotalSeconds);

			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					await RecoverStuckJobs(stoppingToken);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error during job recovery cycle");
				}
				finally
				{
					await Task.Delay(_recoveryInterval, stoppingToken);
				}
			}

			_logger.LogInformation("Job Recovery Service stopped");
		}

		private async Task RecoverStuckJobs(CancellationToken cancellationToken)
		{
			var timeoutUnixTime = _dateTimeProvider.DateTimeOffsetNow.AddMinutes(-_jobTimeoutMinutes).ToUnixTimeSeconds();
			var recoveredCount = await _jobStore.RecoverStuckJobs(
				timeoutUnixTime,
				_maxRetries,
				_retryDelayBaseSeconds,
				cancellationToken);

			if (recoveredCount > 0)
			{
				_logger.LogInformation("Recovered {RecoveredCount} stuck jobs", recoveredCount);
			}
			else
			{
				_logger.LogDebug("No stuck jobs found during recovery check");
			}
		}
	}
}
