using System.Diagnostics;
using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.Infrastructure.Observability;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.UnitTests.JobProcessing;

public class InMemoryJobStoreObservabilityTests
{
	/// <summary>
	/// Verifies that when a job is created successfully, 
	/// the observability interface records the store operation metric.
	/// This ensures proper tracking of store operations.
	/// </summary>
	[Theory, AutoMoqData]
	public async Task CreateJob_Success_RecordsStoreOperationMetric(
		Job job,
		Mock<ILogger<InMemoryJobStore>> mockLogger,
		Mock<IDateTimeProvider> mockDateTimeProvider,
		Mock<IAsyncEndpointsObservability> mockMetrics)
	{
		// Setup the observability to return null for activity (which is what happens in unit tests)
		mockMetrics
			.Setup(x => x.StartStoreOperationActivity(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>()))
			.Returns((Activity?)null);

		// Arrange
		var store = new InMemoryJobStore(mockLogger.Object, mockDateTimeProvider.Object, mockMetrics.Object);

		// Act
		var result = await store.CreateJob(job, CancellationToken.None);

		// Assert
		Assert.True(result.IsSuccess);
		mockMetrics.Verify(m => m.RecordStoreOperation("CreateJob", It.IsAny<string>()), Times.Once);
	}

	/// <summary>
	/// Verifies that when a job retrieval fails due to invalid ID,
	/// the observability interface records the store error metric.
	/// This ensures proper tracking of store operation errors.
	/// </summary>
	[Theory, AutoMoqData]
	public async Task GetJobById_InvalidId_RecordsStoreErrorMetric(
		Mock<ILogger<InMemoryJobStore>> mockLogger,
		Mock<IDateTimeProvider> mockDateTimeProvider,
		Mock<IAsyncEndpointsObservability> mockMetrics)
	{
		// Setup the observability to return null for activity (which is what happens in unit tests)
		mockMetrics
			.Setup(x => x.StartStoreOperationActivity(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>()))
			.Returns((Activity?)null);

		// Arrange
		var store = new InMemoryJobStore(mockLogger.Object, mockDateTimeProvider.Object, mockMetrics.Object);

		// Act
		var result = await store.GetJobById(Guid.Empty, CancellationToken.None);

		// Assert
		Assert.False(result.IsSuccess);
		mockMetrics.Verify(m => m.RecordStoreError("GetJobById", "INVALID_JOB_ID", It.IsAny<string>()), Times.Once);
	}

	/// <summary>
	/// Verifies that when a job update succeeds,
	/// the observability interface records the store operation metric.
	/// This ensures proper tracking of successful store operations.
	/// </summary>
	[Theory, AutoMoqData]
	public async Task UpdateJob_Success_RecordsStoreOperationMetric(
		Job job,
		Mock<ILogger<InMemoryJobStore>> mockLogger,
		Mock<IDateTimeProvider> mockDateTimeProvider,
		Mock<IAsyncEndpointsObservability> mockMetrics)
	{
		// Setup the observability to return null for activity (which is what happens in unit tests)
		mockMetrics
			.Setup(x => x.StartStoreOperationActivity(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>()))
			.Returns((Activity?)null);

		// Arrange
		var store = new InMemoryJobStore(mockLogger.Object, mockDateTimeProvider.Object, mockMetrics.Object);

		// First create the job
		await store.CreateJob(job, CancellationToken.None);

		// Act
		var result = await store.UpdateJob(job, CancellationToken.None);

		// Assert
		Assert.True(result.IsSuccess);
		mockMetrics.Verify(m => m.RecordStoreOperation("UpdateJob", It.IsAny<string>()), Times.AtLeastOnce);
	}
}
