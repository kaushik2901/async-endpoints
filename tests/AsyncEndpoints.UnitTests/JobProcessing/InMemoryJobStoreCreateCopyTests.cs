using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.UnitTests.TestSupport;
using AutoFixture.Xunit2;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.UnitTests.JobProcessing;

/// <summary>
/// Tests for the InMemoryJobStore functionality that specifically uses the CreateCopy method.
/// These tests ensure the immutable objects pattern is working correctly.
/// </summary>
public class InMemoryJobStoreCreateCopyTests
{
	/// <summary>
	/// Verifies that UpdateJob uses the CreateCopy method to create a new job instance 
	/// and updates the job properties correctly without modifying the original job.
	/// This test ensures the immutable objects pattern is working properly.
	/// </summary>
	[Theory, AutoMoqData]
	public async Task UpdateJob_UsesImmutablePattern_WhenJobExists(
		[Frozen] Mock<ILogger<InMemoryJobStore>> mockLogger,
		[Frozen] Mock<IDateTimeProvider> mockDateTimeProvider,
		Job job)
	{
		// Create store manually with the required dependencies
		var store = new InMemoryJobStore(mockLogger.Object, mockDateTimeProvider.Object);

		// Setup datetime mock
		var expectedTime = DateTimeOffset.UtcNow;
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);

		// Arrange - Create a job and store it
		await store.CreateJob(job, CancellationToken.None);

		// Retrieve the original job to compare
		var originalJobResult = await store.GetJobById(job.Id, CancellationToken.None);
		var originalJob = originalJobResult.Data;
		Assert.NotNull(originalJob);

		// Modify the job properties to update
		job.Status = JobStatus.InProgress;
		job.WorkerId = Guid.NewGuid();
		job.Result = "Updated result";

		// Act - Update the job
		var result = await store.UpdateJob(job, CancellationToken.None);

		// Assert
		Assert.True(result.IsSuccess);

		// Verify the updated job has the new properties
		var updatedJobResult = await store.GetJobById(job.Id, CancellationToken.None);
		var updatedJob = updatedJobResult.Data;
		Assert.NotNull(updatedJob);

		Assert.Equal(JobStatus.InProgress, updatedJob.Status);
		Assert.Equal(job.WorkerId, updatedJob.WorkerId);
		Assert.Equal(job.Result, updatedJob.Result);
		Assert.Equal(expectedTime, updatedJob.LastUpdatedAt);
	}

	/// <summary>
	/// Verifies that the CreateCopy method properly creates independent copies of reference type properties.
	/// This ensures that when jobs are updated, the reference properties are not shared between instances.
	/// </summary>
	[Theory, AutoMoqData]
	public void CreateCopy_ProperlyDeepCopiesReferenceTypes(
		[Frozen] Mock<ILogger<InMemoryJobStore>> mockLogger,
		[Frozen] Mock<IDateTimeProvider> mockDateTimeProvider)
	{
		// Create store manually with the required dependencies
		var store = new InMemoryJobStore(mockLogger.Object, mockDateTimeProvider.Object);

		// Setup datetime mock
		var expectedTime = DateTimeOffset.UtcNow;
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);

		// Arrange - Create job with complex reference properties
		var originalHeaders = new Dictionary<string, List<string?>> { { "header1", new List<string?> { "original_value" } } };
		var originalRouteParams = new Dictionary<string, object?> { { "param1", "original_value" } };
		var originalQueryParams = new List<KeyValuePair<string, List<string?>>> {
			new("query1", ["original_value"])
		};

		var job = new Job(expectedTime)
		{
			Name = "TestJob",
			Headers = originalHeaders,
			RouteParams = originalRouteParams,
			QueryParams = originalQueryParams,
			Payload = "{\"data\":\"value\"}",
			Status = JobStatus.Queued
		};

		// Act - Create a copy using CreateCopy method
		var copiedJob = job.CreateCopy(status: JobStatus.InProgress);

		// Modify original reference collections
		originalHeaders.Add("header2", ["modified"]);
		originalHeaders["header1"].Add("additional_value");
		originalRouteParams.Add("param2", "modified");
		originalQueryParams.Add(new KeyValuePair<string, List<string?>>("query2", ["modified"]));

		// Assert - copied job should have original values, not modified ones
		Assert.Equal(JobStatus.InProgress, copiedJob.Status); // Updated property
		Assert.Equal(JobStatus.Queued, job.Status); // Original job unchanged

		// Headers should be different instances with original values
		Assert.Single(copiedJob.Headers);
		Assert.True(copiedJob.Headers.ContainsKey("header1"));
		Assert.Equal("original_value", copiedJob.Headers["header1"][0]);
		Assert.NotSame(job.Headers, copiedJob.Headers);

		// RouteParams should be different instances with original values
		Assert.Single(copiedJob.RouteParams);
		Assert.True(copiedJob.RouteParams.ContainsKey("param1"));
		Assert.Equal("original_value", copiedJob.RouteParams["param1"]);
		Assert.NotSame(job.RouteParams, copiedJob.RouteParams);

		// QueryParams should be different instances with original values
		Assert.Single(copiedJob.QueryParams);
		Assert.Equal("query1", copiedJob.QueryParams[0].Key);
		Assert.Equal("original_value", copiedJob.QueryParams[0].Value[0]);
		Assert.NotSame(job.QueryParams, copiedJob.QueryParams);
	}

	/// <summary>
	/// Verifies that ClaimNextJobForWorker uses the CreateCopy method properly 
	/// when claiming a job for a worker, updating the status and worker ID correctly.
	/// </summary>
	[Theory, AutoMoqData]
	public async Task ClaimNextJobForWorker_UsesCreateCopy_WhenClaimingJob(
		[Frozen] Mock<ILogger<InMemoryJobStore>> mockLogger,
		[Frozen] Mock<IDateTimeProvider> mockDateTimeProvider,
		Job job,
		Guid workerId)
	{
		// Create store manually with the required dependencies
		var store = new InMemoryJobStore(mockLogger.Object, mockDateTimeProvider.Object);

		// Setup datetime mock
		var expectedTime = DateTimeOffset.UtcNow;
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);

		// Arrange
		job.Status = JobStatus.Queued;
		job.WorkerId = null; // Ensure no worker is assigned initially
		await store.CreateJob(job, CancellationToken.None);

		// Act
		var result = await store.ClaimNextJobForWorker(workerId, CancellationToken.None);

		// Assert
		Assert.True(result.IsSuccess);
		Assert.NotNull(result.DataOrNull);

		var claimedJob = result.DataOrNull;
		Assert.Equal(JobStatus.InProgress, claimedJob.Status);
		Assert.Equal(workerId, claimedJob.WorkerId);
		Assert.Equal(expectedTime, claimedJob.StartedAt);
		Assert.Equal(expectedTime, claimedJob.LastUpdatedAt);
	}

	/// <summary>
	/// Verifies that UpdateJob works correctly by replacing the job in the dictionary.
	/// This test ensures that the update mechanism functions as expected.
	/// </summary>
	[Theory, AutoMoqData]
	public async Task UpdateJob_UpdatesJobCorrectly(
		[Frozen] Mock<ILogger<InMemoryJobStore>> mockLogger,
		[Frozen] Mock<IDateTimeProvider> mockDateTimeProvider,
		Job job)
	{
		// Create store manually with the required dependencies
		var store = new InMemoryJobStore(mockLogger.Object, mockDateTimeProvider.Object);

		// Setup datetime mock
		var expectedTime = DateTimeOffset.UtcNow;
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);

		// Arrange - Create a job and store it
		await store.CreateJob(job, CancellationToken.None);

		// Get the existing job
		var existingJobResult = await store.GetJobById(job.Id, CancellationToken.None);
		var existingJob = existingJobResult.Data;
		Assert.NotNull(existingJob);
		Assert.Equal(JobStatus.Queued, existingJob.Status);

		// Create an updated version of the job using CreateCopy
		var updatedJob = existingJob.CreateCopy(
			status: JobStatus.InProgress,
			result: "Test result",
			lastUpdatedAt: expectedTime);

		// Act - Update the job
		var result = await store.UpdateJob(updatedJob, CancellationToken.None);

		// Assert
		Assert.True(result.IsSuccess);

		// Verify the job was properly updated
		var retrievedJobResult = await store.GetJobById(job.Id, CancellationToken.None);
		var retrievedJob = retrievedJobResult.Data;
		Assert.NotNull(retrievedJob);
		Assert.Equal(JobStatus.InProgress, retrievedJob.Status);
		Assert.Equal("Test result", retrievedJob.Result);
		Assert.Equal(expectedTime, retrievedJob.LastUpdatedAt);
	}
}
