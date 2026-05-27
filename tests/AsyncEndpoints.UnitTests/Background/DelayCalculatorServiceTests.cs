using AsyncEndpoints.Background;
using AsyncEndpoints.Configuration;
using AsyncEndpoints.UnitTests.TestSupport;
using AsyncEndpoints.Worker.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.UnitTests.Background;

public class DelayCalculatorServiceTests
{
	[Theory, AutoMoqData]
	public void Constructor_Succeeds_WithValidDependencies(
		ILogger<DelayCalculatorService> logger)
	{
		// Arrange
		var workerOptions = new WorkerOptions();

		// Act
		var service = new DelayCalculatorService(logger, workerOptions);

		// Assert
		Assert.NotNull(service);
	}

	[Theory, AutoMoqData]
	public void CalculateDelay_ReturnsBasePollingInterval_WhenJobSuccessfullyEnqueued(
		ILogger<DelayCalculatorService> logger)
	{
		// Arrange
		var workerOptions = new WorkerOptions { PollingIntervalMin = TimeSpan.FromMilliseconds(1000) };

		var service = new DelayCalculatorService(logger, workerOptions);

		// Act
		var result = service.CalculateDelay(JobClaimingState.JobSuccessfullyEnqueued, workerOptions);

		// Assert
		Assert.Equal(TimeSpan.FromMilliseconds(1000), result);
	}

	[Theory, AutoMoqData]
	public void CalculateDelay_ReturnsIncreasedDelay_WhenNoJobFound(
		ILogger<DelayCalculatorService> logger)
	{
		// Arrange
		var workerOptions = new WorkerOptions { PollingIntervalMin = TimeSpan.FromMilliseconds(1000) };

		var service = new DelayCalculatorService(logger, workerOptions);

		// Act
		var result = service.CalculateDelay(JobClaimingState.NoJobFound, workerOptions);

		// Assert
		Assert.Equal(TimeSpan.FromMilliseconds(3000), result);
	}

	[Theory, AutoMoqData]
	public void CalculateDelay_ReturnsDoubleDelay_WhenFailedToEnqueue(
		ILogger<DelayCalculatorService> logger)
	{
		// Arrange
		var workerOptions = new WorkerOptions { PollingIntervalMin = TimeSpan.FromMilliseconds(1000) };

		var service = new DelayCalculatorService(logger, workerOptions);

		// Act
		var result = service.CalculateDelay(JobClaimingState.FailedToEnqueue, workerOptions);

		// Assert
		Assert.Equal(TimeSpan.FromMilliseconds(2000), result);
	}

	[Theory, AutoMoqData]
	public void CalculateDelay_ReturnsErrorDelay_WhenErrorOccurred(
		ILogger<DelayCalculatorService> logger)
	{
		// Arrange
		var workerOptions = new WorkerOptions();

		var service = new DelayCalculatorService(logger, workerOptions);

		// Act
		var result = service.CalculateDelay(JobClaimingState.ErrorOccurred, workerOptions);

		// Assert
		Assert.Equal(TimeSpan.FromSeconds(AsyncEndpointsConstants.JobProducerErrorDelaySeconds), result);
	}

	[Theory, AutoMoqData]
	public void CalculateDelay_ReturnsBaseInterval_WhenUnknownState(
		ILogger<DelayCalculatorService> logger)
	{
		// Arrange
		var workerOptions = new WorkerOptions { PollingIntervalMin = TimeSpan.FromMilliseconds(1000) };

		var service = new DelayCalculatorService(logger, workerOptions);

		// Act
		var result = service.CalculateDelay((JobClaimingState)(-1), workerOptions); // Unknown state

		// Assert
		Assert.Equal(TimeSpan.FromMilliseconds(1000), result);
	}
}
