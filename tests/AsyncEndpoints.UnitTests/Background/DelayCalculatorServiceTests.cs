using AsyncEndpoints.Background;
using AsyncEndpoints.Configuration;
using AsyncEndpoints.UnitTests.TestSupport;
using AutoFixture.Xunit2;
using Microsoft.Extensions.Options;
using Moq;

namespace AsyncEndpoints.UnitTests.Background;

public class DelayCalculatorServiceTests
{
	[Theory, AutoMoqData]
	public void Constructor_Succeeds_WithValidDependencies(
		Mock<IOptions<AsyncEndpointsConfigurations>> mockOptions)
	{
		// Arrange
		var configurations = new AsyncEndpointsConfigurations { WorkerConfigurations = new AsyncEndpointsWorkerConfigurations() };
		mockOptions.Setup(x => x.Value).Returns(configurations);

		// Act
		var service = new DelayCalculatorService(mockOptions.Object);

		// Assert
		Assert.NotNull(service);
	}

	[Theory, AutoMoqData]
	public void CalculateDelay_ReturnsBasePollingInterval_WhenJobSuccessfullyEnqueued(
		Mock<IOptions<AsyncEndpointsConfigurations>> mockOptions,
		AsyncEndpointsWorkerConfigurations workerConfigurations)
	{
		// Arrange
		var configurations = new AsyncEndpointsConfigurations { WorkerConfigurations = workerConfigurations };
		mockOptions.Setup(x => x.Value).Returns(configurations);
		workerConfigurations.PollingIntervalMs = 1000; // 1 second

		var service = new DelayCalculatorService(mockOptions.Object);

		// Act
		var result = service.CalculateDelay(JobClaimingState.JobSuccessfullyEnqueued, workerConfigurations);

		// Assert
		Assert.Equal(TimeSpan.FromMilliseconds(1000), result);
	}

	[Theory, AutoMoqData]
	public void CalculateDelay_ReturnsIncreasedDelay_WhenNoJobFound(
		Mock<IOptions<AsyncEndpointsConfigurations>> mockOptions,
		AsyncEndpointsWorkerConfigurations workerConfigurations)
	{
		// Arrange
		var configurations = new AsyncEndpointsConfigurations { WorkerConfigurations = workerConfigurations };
		mockOptions.Setup(x => x.Value).Returns(configurations);
		workerConfigurations.PollingIntervalMs = 1000; // 1 second

		var service = new DelayCalculatorService(mockOptions.Object);

		// Act
		var result = service.CalculateDelay(JobClaimingState.NoJobFound, workerConfigurations);

		// Assert
		// NoJobFound should return min(pollingInterval * 3, maxDelay), so 3 seconds in this case
		Assert.Equal(TimeSpan.FromMilliseconds(3000), result);
	}

	[Theory, AutoMoqData]
	public void CalculateDelay_ReturnsDoubleDelay_WhenFailedToEnqueue(
		Mock<IOptions<AsyncEndpointsConfigurations>> mockOptions,
		AsyncEndpointsWorkerConfigurations workerConfigurations)
	{
		// Arrange
		var configurations = new AsyncEndpointsConfigurations { WorkerConfigurations = workerConfigurations };
		mockOptions.Setup(x => x.Value).Returns(configurations);
		workerConfigurations.PollingIntervalMs = 1000; // 1 second

		var service = new DelayCalculatorService(mockOptions.Object);

		// Act
		var result = service.CalculateDelay(JobClaimingState.FailedToEnqueue, workerConfigurations);

		// Assert
		Assert.Equal(TimeSpan.FromMilliseconds(2000), result);
	}

	[Theory, AutoMoqData]
	public void CalculateDelay_ReturnsErrorDelay_WhenErrorOccurred(
		Mock<IOptions<AsyncEndpointsConfigurations>> mockOptions,
		AsyncEndpointsWorkerConfigurations workerConfigurations)
	{
		// Arrange
		var configurations = new AsyncEndpointsConfigurations { WorkerConfigurations = workerConfigurations };
		mockOptions.Setup(x => x.Value).Returns(configurations);

		var service = new DelayCalculatorService(mockOptions.Object);

		// Act
		var result = service.CalculateDelay(JobClaimingState.ErrorOccurred, workerConfigurations);

		// Assert
		Assert.Equal(TimeSpan.FromSeconds(AsyncEndpointsConstants.JobProducerErrorDelaySeconds), result);
	}

	[Theory, AutoMoqData]
	public void CalculateDelay_ReturnsBaseInterval_WhenUnknownState(
		Mock<IOptions<AsyncEndpointsConfigurations>> mockOptions,
		AsyncEndpointsWorkerConfigurations workerConfigurations)
	{
		// Arrange
		var configurations = new AsyncEndpointsConfigurations { WorkerConfigurations = workerConfigurations };
		mockOptions.Setup(x => x.Value).Returns(configurations);
		workerConfigurations.PollingIntervalMs = 1000; // 1 second

		var service = new DelayCalculatorService(mockOptions.Object);

		// Act
		var result = service.CalculateDelay((JobClaimingState)(-1), workerConfigurations); // Unknown state

		// Assert
		Assert.Equal(TimeSpan.FromMilliseconds(1000), result);
	}
}
