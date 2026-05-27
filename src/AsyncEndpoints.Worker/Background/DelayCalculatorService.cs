using AsyncEndpoints.Configuration;
using AsyncEndpoints.Worker.Hosting;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.Background;

/// <inheritdoc />
public class DelayCalculatorService(ILogger<DelayCalculatorService> logger, WorkerOptions workerOptions) : IDelayCalculatorService
{
	private readonly ILogger<DelayCalculatorService> _logger = logger;
	private readonly TimeSpan _basePollingInterval = workerOptions.PollingIntervalMin;

	/// <inheritdoc />
	public TimeSpan CalculateDelay(JobClaimingState state, WorkerOptions workerOptions)
	{
		var baseMs = (long)workerOptions.PollingIntervalMin.TotalMilliseconds;
		var maxMs = AsyncEndpointsConstants.JobProducerMaxDelayMs;
		var delay = state switch
		{
			JobClaimingState.JobSuccessfullyEnqueued => _basePollingInterval,
			JobClaimingState.NoJobFound => TimeSpan.FromMilliseconds(
				Math.Min(baseMs * 3, maxMs)),
			JobClaimingState.FailedToEnqueue => TimeSpan.FromMilliseconds(
				baseMs * 2),
			JobClaimingState.ErrorOccurred => TimeSpan.FromSeconds(
				AsyncEndpointsConstants.JobProducerErrorDelaySeconds),
			_ => _basePollingInterval // Default case
		};

		_logger.LogDebug("Calculated delay for state {State}: {Delay}ms", state, delay.TotalMilliseconds);

		return delay;
	}
}
