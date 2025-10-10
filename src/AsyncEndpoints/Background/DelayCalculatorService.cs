using System;
using AsyncEndpoints.Configuration;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.Background;

/// <inheritdoc />
public class DelayCalculatorService(IOptions<AsyncEndpointsConfigurations> configurations) : IDelayCalculatorService
{
	private readonly TimeSpan _basePollingInterval = TimeSpan.FromMilliseconds(configurations.Value.WorkerConfigurations.PollingIntervalMs);

	/// <inheritdoc />
	public TimeSpan CalculateDelay(JobClaimingState state, AsyncEndpointsWorkerConfigurations workerConfigurations)
	{
		return state switch
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
	}
}
