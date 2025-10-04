using System;
using AsyncEndpoints.Configuration;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.Background;

/// <summary>
/// Implements the IDelayCalculatorService to calculate appropriate delays based on the current state
/// </summary>
public class DelayCalculatorService(IOptions<AsyncEndpointsConfigurations> configurations) : IDelayCalculatorService
{
	private readonly TimeSpan _basePollingInterval = TimeSpan.FromMilliseconds(configurations.Value.WorkerConfigurations.PollingIntervalMs);

	/// <summary>
	/// Calculates the appropriate delay based on the current state and base polling interval
	/// </summary>
	/// <param name="state">The current state of job processing</param>
	/// <param name="workerConfigurations">The worker configurations</param>
	/// <returns>The calculated delay as a TimeSpan</returns>
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
