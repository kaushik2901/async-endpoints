using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.UnitTests.TestSupport;
using AutoFixture.Xunit2;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.UnitTests.InMemoryStore;

public class InMemoryJobStoreTests
{
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