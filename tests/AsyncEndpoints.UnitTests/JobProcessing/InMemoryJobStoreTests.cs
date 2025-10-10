using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.UnitTests.TestSupport;
using AutoFixture.Xunit2;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.UnitTests.InMemoryStore;

public class InMemoryJobStoreTests
{
	/// <summary>
	/// Verifies that the InMemoryJobStore can be constructed with valid dependencies without throwing an exception.
	/// This test ensures the constructor properly accepts and stores the required dependencies.
	/// </summary>
	[Theory, AutoMoqData]
	public void Constructor_Succeeds_WithValidDependencies(
		Mock<ILogger<InMemoryJobStore>> mockLogger,
		Mock<IDateTimeProvider> mockDateTimeProvider)
	{
		// Act
		var store = new InMemoryJobStore(mockLogger.Object, mockDateTimeProvider.Object);

		// Assert
		Assert.NotNull(store);
	}

	/// <summary>
	/// Verifies that the InMemoryJobStore can create a new job when it doesn't already exist.
	/// This test ensures the basic job creation functionality works correctly.
	/// </summary>
	[Theory, AutoMoqData]
	public async Task CreateJob_Succeeds_WhenJobDoesNotExist(
		[Frozen] InMemoryJobStore store,
		Job job)
	{
		// Act
		var result = await store.CreateJob(job, CancellationToken.None);

		// Assert
		Assert.True(result.IsSuccess);
		// The Data property doesn't exist on MethodResult (only MethodResult<T>), 
		// So let's verify the job was created by getting it back
		var getResult = await store.GetJobById(job.Id, CancellationToken.None);
		Assert.True(getResult.IsSuccess);
		Assert.NotNull(getResult.Data);
	}

	/// <summary>
	/// Verifies that the InMemoryJobStore properly handles attempts to create a job that already exists.
	/// This test ensures duplicate job creation attempts are rejected appropriately.
	/// </summary>
	[Theory, AutoMoqData]
	public async Task CreateJob_Fails_WhenJobAlreadyExists(
		[Frozen] InMemoryJobStore store,
		Job job)
	{
		// Arrange
		await store.CreateJob(job, CancellationToken.None);

		// Act
		var result = await store.CreateJob(job, CancellationToken.None);

		// Assert
		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
	}

	/// <summary>
	/// Verifies that the InMemoryJobStore can retrieve a job by its ID when the job exists.
	/// This test ensures basic job retrieval functionality works correctly.
	/// </summary>
	[Theory, AutoMoqData]
	public async Task GetJobById_ReturnsJob_WhenJobExists(
		[Frozen] InMemoryJobStore store,
		Job job)
	{
		// Arrange
		await store.CreateJob(job, CancellationToken.None);

		// Act
		var result = await store.GetJobById(job.Id, CancellationToken.None);

		// Assert
		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Data);
		Assert.Equal(job, result.Data);
	}

	/// <summary>
	/// Verifies that the InMemoryJobStore returns a failure when attempting to retrieve a non-existent job.
	/// This test ensures proper error handling for missing jobs.
	/// </summary>
	[Theory, AutoMoqData]
	public async Task GetJobById_ReturnsFailure_WhenJobDoesNotExist(
		[Frozen] InMemoryJobStore store,
		Guid jobId)
	{
		// Act
		var result = await store.GetJobById(jobId, CancellationToken.None);

		// Assert
		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
	}

	/// <summary>
	/// Verifies that the InMemoryJobStore can update an existing job's properties.
	/// This test ensures the job update functionality works correctly for valid jobs.
	/// </summary>
	[Theory, AutoMoqData]
	public async Task UpdateJob_Succeeds_WhenJobExists(
		[Frozen] Mock<ILogger<InMemoryJobStore>> mockLogger,
		[Frozen] Mock<IDateTimeProvider> mockDateTimeProvider,
		Job job)
	{
		// Create store manually with the required dependencies
		var store = new InMemoryJobStore(mockLogger.Object, mockDateTimeProvider.Object);

		// Setup datetime mock
		var expectedTime = DateTimeOffset.UtcNow;
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);

		// Arrange
		await store.CreateJob(job, CancellationToken.None);
		job.UpdateStatus(JobStatus.InProgress, mockDateTimeProvider.Object);

		// Act
		var result = await store.UpdateJob(job, CancellationToken.None);

		// Assert
		Assert.True(result.IsSuccess);
	}

	/// <summary>
	/// Verifies that the InMemoryJobStore returns a failure when attempting to update a non-existent job.
	/// This test ensures proper error handling for update attempts on missing jobs.
	/// </summary>
	[Theory, AutoMoqData]
	public async Task UpdateJob_Fails_WhenJobDoesNotExist(
		[Frozen] InMemoryJobStore store,
		Job job)
	{
		// Act
		var result = await store.UpdateJob(job, CancellationToken.None);

		// Assert
		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
	}

	/// <summary>
	/// Verifies that the InMemoryJobStore can claim the next available job for a worker when jobs exist.
	/// This test ensures the job claiming functionality works correctly, updating the job status and assigning it to the worker.
	/// </summary>
	[Theory, AutoMoqData]
	public async Task ClaimNextJobForWorker_ReturnsAvailableJob_WhenJobExists(
		[Frozen] Mock<ILogger<InMemoryJobStore>> mockLogger,
		[Frozen] Mock<IDateTimeProvider> mockDateTimeProvider,
		Job job1,
		Job job2,
		Guid workerId)
	{
		// Create store manually with the required dependencies
		var store = new InMemoryJobStore(mockLogger.Object, mockDateTimeProvider.Object);

		// Setup datetime mock
		var expectedTime = DateTimeOffset.UtcNow;
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);

		// Arrange
		job1.UpdateStatus(JobStatus.Queued, mockDateTimeProvider.Object);
		job2.UpdateStatus(JobStatus.Queued, mockDateTimeProvider.Object);
		// Ensure the jobs have no worker assigned initially
		job1.WorkerId = null;
		job2.WorkerId = null;
		await store.CreateJob(job1, CancellationToken.None);
		await store.CreateJob(job2, CancellationToken.None);

		// Act
		var result = await store.ClaimNextJobForWorker(workerId, CancellationToken.None);

		// Assert
		Assert.True(result.IsSuccess);
		Assert.NotNull(result.DataOrNull);
		Assert.Equal(JobStatus.InProgress, result.DataOrNull.Status);
		Assert.Equal(workerId, result.DataOrNull.WorkerId);
	}

	/// <summary>
	/// Verifies that the InMemoryJobStore returns null when no jobs are available for a worker to claim.
	/// This test ensures proper handling of empty queues in the job claiming process.
	/// </summary>
	[Theory, AutoMoqData]
	public async Task ClaimNextJobForWorker_ReturnsNull_WhenNoJobsAvailable(
		[Frozen] InMemoryJobStore store,
		Guid workerId)
	{
		// Act
		var result = await store.ClaimNextJobForWorker(workerId, CancellationToken.None);

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Null(result.DataOrNull);
	}
}
