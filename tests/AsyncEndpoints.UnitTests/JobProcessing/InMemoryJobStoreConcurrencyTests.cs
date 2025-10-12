using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.UnitTests.TestSupport;
using AutoFixture.Xunit2;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.UnitTests.JobProcessing;

/// <summary>
/// Tests for the InMemoryJobStore concurrent update behavior to ensure race conditions are handled properly.
/// These tests verify that the immutable objects pattern and TryUpdate mechanism work correctly.
/// </summary>
public class InMemoryJobStoreConcurrencyTests
{
	/// <summary>
	/// Verifies that concurrent UpdateJob operations do not cause data corruption
	/// and that the atomic update mechanism works properly.
	/// </summary>
	[Theory, AutoMoqData]
	public async Task UpdateJob_HandlesConcurrentUpdates(
		[Frozen] Mock<ILogger<InMemoryJobStore>> mockLogger,
		[Frozen] Mock<IDateTimeProvider> mockDateTimeProvider)
	{
		// Create store manually with the required dependencies
		var store = new InMemoryJobStore(mockLogger.Object, mockDateTimeProvider.Object);

		// Setup datetime mock
		var expectedTime = DateTimeOffset.UtcNow;
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);

		// Arrange - Create a job
		var job = Job.Create(Guid.NewGuid(), "TestJob", "{\"data\":\"value\"}", mockDateTimeProvider.Object);
		await store.CreateJob(job, CancellationToken.None);

		// Simulate two concurrent updates to the same job
		var firstUpdateTask = Task.Run(async () =>
		{
			var jobToUpdate = (await store.GetJobById(job.Id, CancellationToken.None)).Data!;
			var updatedJob = jobToUpdate.CreateCopy(
				status: JobStatus.InProgress,
				result: "Result from first update",
				lastUpdatedAt: expectedTime
			);
			return await store.UpdateJob(updatedJob, CancellationToken.None);
		});

		var secondUpdateTask = Task.Run(async () =>
		{
			var jobToUpdate = (await store.GetJobById(job.Id, CancellationToken.None)).Data!;
			var updatedJob = jobToUpdate.CreateCopy(
				status: JobStatus.Failed,
				result: "Result from second update",
				lastUpdatedAt: expectedTime
			);
			return await store.UpdateJob(updatedJob, CancellationToken.None);
		});

		// Act
		var results = await Task.WhenAll(firstUpdateTask, secondUpdateTask);

		// Assert
		// At least one of the updates should succeed, and the other might fail due to conflict
		var successfulUpdates = results.Count(r => r.IsSuccess);
		Assert.True(successfulUpdates >= 1, "At least one update should succeed");

		// Get the final state of the job
		var finalResult = await store.GetJobById(job.Id, CancellationToken.None);
		var finalJob = finalResult.Data;
		Assert.NotNull(finalJob);

		// Verify the final job state is one of the attempted states
		Assert.True(finalJob.Status == JobStatus.InProgress || finalJob.Status == JobStatus.Failed);
		Assert.Equal(expectedTime, finalJob.LastUpdatedAt);
	}

	/// <summary>
	/// Verifies that ClaimNextJobForWorker properly handles race conditions when multiple workers 
	/// try to claim the same job simultaneously.
	/// </summary>
	[Theory, AutoMoqData]
	public async Task ClaimNextJobForWorker_HandlesMultipleWorkers(
		[Frozen] Mock<ILogger<InMemoryJobStore>> mockLogger,
		[Frozen] Mock<IDateTimeProvider> mockDateTimeProvider,
		Job job)
	{
		// Create store manually with the required dependencies
		var store = new InMemoryJobStore(mockLogger.Object, mockDateTimeProvider.Object);

		// Setup datetime mock
		var expectedTime = DateTimeOffset.UtcNow;
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);

		// Arrange - Create a single job available for claiming
		job.Status = JobStatus.Queued;
		job.WorkerId = null;
		await store.CreateJob(job, CancellationToken.None);

		// Simulate multiple workers trying to claim the same job simultaneously
		var worker1Id = Guid.NewGuid();
		var worker2Id = Guid.NewGuid();
		var worker3Id = Guid.NewGuid();

		var claimTask1 = store.ClaimNextJobForWorker(worker1Id, CancellationToken.None);
		var claimTask2 = store.ClaimNextJobForWorker(worker2Id, CancellationToken.None);
		var claimTask3 = store.ClaimNextJobForWorker(worker3Id, CancellationToken.None);

		// Act
		var claimResults = await Task.WhenAll(claimTask1, claimTask2, claimTask3);

		// Assert
		var successfulClaims = claimResults.Where(r => r.IsSuccess && r.DataOrNull != null).ToList();
		var failedClaims = claimResults.Where(r => r.IsSuccess && r.DataOrNull == null).ToList();

		// Exactly one worker should successfully claim the job
		Assert.Single(successfulClaims);
		// The other two should get null (no job available)
		Assert.Equal(2, failedClaims.Count);

		// Verify that the successfully claimed job has the correct properties
		var claimedJob = successfulClaims.First().DataOrNull;
		Assert.NotNull(claimedJob);
		Assert.Equal(JobStatus.InProgress, claimedJob.Status);
		var workerIds = new[] { worker1Id, worker2Id, worker3Id };
		Assert.Contains(claimedJob.WorkerId!.Value, workerIds);
		Assert.Equal(expectedTime, claimedJob.StartedAt);
		Assert.Equal(expectedTime, claimedJob.LastUpdatedAt);
	}
}
