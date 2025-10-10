using AsyncEndpoints.Background;
using AsyncEndpoints.Configuration;
using AsyncEndpoints.UnitTests.TestSupport;
using Microsoft.Extensions.Options;
using Moq;

namespace AsyncEndpoints.UnitTests.Background;

public class DelayCalculatorServiceTests
{
	/// <summary>
	/// Verifies that the DelayCalculatorService can be constructed with valid dependencies without throwing an exception.
	/// This test ensures the constructor properly accepts and stores the required IOptions dependency.
	/// </summary>
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

	/// <summary>
	/// Verifies that when a job is successfully enqueued, the delay calculator returns the base polling interval.
	/// This ensures efficient processing when work is available in the queue.
	/// </summary>
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

	/// <summary>
	/// Verifies that when no job is found in the queue, the delay calculator returns an increased delay (3x polling interval).
	/// This helps reduce resource consumption when the queue is empty while still checking periodically.
	/// </summary>
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

	/// <summary>
	/// Verifies that when a job fails to be enqueued, the delay calculator returns a doubled delay.
	/// This provides backoff mechanism when there are temporary issues with job processing.
	/// </summary>
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

	/// <summary>
	/// Verifies that when an error occurs during job processing, the delay calculator returns a predefined error delay.
	/// This provides appropriate backoff for handling error conditions in the system.
	/// </summary>
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

	/// <summary>
	/// Verifies that when an unknown job claiming state is provided, the delay calculator defaults to the base polling interval.
	/// This provides safe fallback behavior for unexpected states.
	/// </summary>
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
