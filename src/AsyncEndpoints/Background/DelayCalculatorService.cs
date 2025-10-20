using System;
using AsyncEndpoints.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.Background;

/// <inheritdoc />
public class DelayCalculatorService(ILogger<DelayCalculatorService> logger, IOptions<AsyncEndpointsConfigurations> configurations) : IDelayCalculatorService
{
	private readonly ILogger<DelayCalculatorService> _logger = logger;
	private readonly TimeSpan _basePollingInterval = TimeSpan.FromMilliseconds(configurations.Value.WorkerConfigurations.PollingIntervalMs);

	/// <inheritdoc />
	public TimeSpan CalculateDelay(JobClaimingState state, AsyncEndpointsWorkerConfigurations workerConfigurations)
	{
		var delay = state switch
		{
			JobClaimingState.JobSuccessfullyEnqueued => _basePollingInterval,
			JobClaimingState.NoJobFound => TimeSpan.FromMilliseconds(
				Math.Min(workerConfigurations.PollingIntervalMs * 3, AsyncEndpointsConstants.JobProducerMaxDelayMs)),
			JobClaimingState.FailedToEnqueue => TimeSpan.FromMilliseconds(
				workerConfigurations.PollingIntervalMs * 2),
			JobClaimingState.ErrorOccurred => TimeSpan.FromSeconds(
				AsyncEndpointsConstants.JobProducerErrorDelaySeconds),
			_ => _basePollingInterval // Default case
		};

		_logger.LogDebug("Calculated delay for state {State}: {Delay}ms", state, delay.TotalMilliseconds);

		return delay;
	}
}
