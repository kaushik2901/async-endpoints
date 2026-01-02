using System.Diagnostics;
using AsyncEndpoints.Configuration;
using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.Infrastructure.Observability;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.UnitTests.TestSupport;
using AsyncEndpoints.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AsyncEndpoints.UnitTests.JobProcessing;

public class JobManagerObservabilityTests
{
	/// <summary>
	/// Verifies that when a new job is submitted, the observability interface records the job creation metric.
	/// This ensures proper metric collection for monitoring job creation rates.
	/// </summary>
	[Theory, AutoMoqData]
	public async Task SubmitJob_CreatesNewJob_RecordsJobCreatedMetric(
		string jobName,
		string payload,
		Mock<IJobStore> mockJobStore,
		Mock<ILogger<JobManager>> mockLogger,
		Mock<IOptions<AsyncEndpointsConfigurations>> mockOptions,
		Mock<IDateTimeProvider> mockDateTimeProvider,
		Mock<IAsyncEndpointsObservability> mockMetrics,
		Job job)
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Headers[AsyncEndpointsConstants.JobIdHeaderName] = job.Id.ToString();

		// Setup the options to return proper configurations
		var config = new AsyncEndpointsConfigurations();
		mockOptions.Setup(x => x.Value).Returns(config);

		// Setup the observability to return null for activity (which is what happens in unit tests)
		mockMetrics
			.Setup(x => x.StartJobSubmitActivity(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>()))
			.Returns((Activity?)null);

		mockJobStore.Setup(store => store.GetJobById(job.Id, It.IsAny<CancellationToken>()))
			.ReturnsAsync(MethodResult<Job>.Failure(AsyncEndpointError.FromCode("JOB_NOT_FOUND", $"Job with ID {job.Id} not found"))); // No existing job found
		mockJobStore.Setup(store => store.CreateJob(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(MethodResult.Success);

		var jobManager = new JobManager(
			mockJobStore.Object,
			mockLogger.Object,
			mockOptions.Object,
			mockDateTimeProvider.Object,
			mockMetrics.Object);

		// Act
		await jobManager.SubmitJob(jobName, payload, httpContext, CancellationToken.None);

		// Assert
		mockMetrics.Verify(m => m.RecordJobCreated(jobName, It.IsAny<string>()), Times.Once);
	}

	/// <summary>
	/// Verifies that when job processing fails and max retries are exceeded, 
	/// the observability interface records the job failure metric.
	/// This ensures proper failure tracking for monitoring system health.
	/// </summary>
	[Theory, AutoMoqData]
	public async Task ProcessJobFailure_MaxRetriesExceeded_RecordsJobFailedMetric(
		Guid jobId,
		Mock<IJobStore> mockJobStore,
		Mock<ILogger<JobManager>> mockLogger,
		Mock<IOptions<AsyncEndpointsConfigurations>> mockOptions,
		Mock<IDateTimeProvider> mockDateTimeProvider,
		Mock<IAsyncEndpointsObservability> mockMetrics,
		AsyncEndpointError error)
	{
		// Arrange
		var config = new AsyncEndpointsConfigurations();
		mockOptions.Setup(x => x.Value).Returns(config);

		var job = Job.Create(
			jobId,
			"TestJob",
			"{}",
			[],
			[],
			[],
			5, // MaxRetries = 5
			mockDateTimeProvider.Object);

		// Manually set the properties that were set in the original test
		job = job.CreateCopy(
			retryCount: 5,
			lastUpdatedAt: DateTimeOffset.UtcNow,
			dateTimeProvider: mockDateTimeProvider.Object);
		mockJobStore.Setup(store => store.GetJobById(jobId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(MethodResult<Job>.Success(job));
		mockJobStore.Setup(store => store.UpdateJob(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(MethodResult.Success);

		var jobManager = new JobManager(
			mockJobStore.Object,
			mockLogger.Object,
			mockOptions.Object,
			mockDateTimeProvider.Object,
			mockMetrics.Object);

		// Act
		await jobManager.ProcessJobFailure(jobId, error, CancellationToken.None);

		// Assert
		mockMetrics.Verify(m => m.RecordJobFailed(job.Name, error.Code, It.IsAny<string>()), Times.Once);
	}

	/// <summary>
	/// Verifies that when job processing fails but retries are still available,
	/// the observability interface records the retry metric.
	/// This ensures proper tracking of retry behavior in the system.
	/// </summary>
	[Theory, AutoMoqData]
	public async Task ProcessJobFailure_RetriesAvailable_RecordsRetryMetric(
		Guid jobId,
		Mock<IJobStore> mockJobStore,
		Mock<ILogger<JobManager>> mockLogger,
		Mock<IOptions<AsyncEndpointsConfigurations>> mockOptions,
		Mock<IDateTimeProvider> mockDateTimeProvider,
		Mock<IAsyncEndpointsObservability> mockMetrics,
		AsyncEndpointError error)
	{
		// Arrange
		var config = new AsyncEndpointsConfigurations();
		mockOptions.Setup(x => x.Value).Returns(config);

		var job = Job.Create(
			jobId,
			"TestJob",
			"{}",
			[],
			[],
			[],
			5, // MaxRetries = 5
			mockDateTimeProvider.Object);

		// Manually set the properties that were set in the original test
		job = job.CreateCopy(
			retryCount: 1,
			lastUpdatedAt: DateTimeOffset.UtcNow,
			dateTimeProvider: mockDateTimeProvider.Object);
		mockJobStore.Setup(store => store.GetJobById(jobId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(MethodResult<Job>.Success(job));
		mockJobStore.Setup(store => store.UpdateJob(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(MethodResult.Success);

		var jobManager = new JobManager(
			mockJobStore.Object,
			mockLogger.Object,
			mockOptions.Object,
			mockDateTimeProvider.Object,
			mockMetrics.Object);

		// Act
		await jobManager.ProcessJobFailure(jobId, error, CancellationToken.None);

		// Assert
		mockMetrics.Verify(m => m.RecordJobRetries(job.Name, It.IsAny<string>()), Times.Once);
	}
}
