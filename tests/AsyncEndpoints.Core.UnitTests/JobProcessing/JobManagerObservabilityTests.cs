using AsyncEndpoints.Abstractions.Utilities;
using AsyncEndpoints.Core.Configuration;
using AsyncEndpoints.Core.Legacy.JobProcessing;
using AsyncEndpoints.Core.Legacy.Observability;
using AsyncEndpoints.Core.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics;

namespace AsyncEndpoints.Core.UnitTests.JobProcessing;

public class JobManagerObservabilityTests
{
	[Theory, AutoMoqData]
	public async Task SubmitJob_CreatesNewJob_RecordsJobCreatedMetric(
		string jobName,
		string payload,
		Mock<IJobStore> mockJobStore,
		Mock<ILogger<JobManager>> mockLogger,
		Mock<TimeProvider> mockDateTimeProvider,
		Mock<IAsyncEndpointsObservability> mockMetrics,
		Job job)
	{
		var jobId = job.Id;

		var options = new AsyncEndpointsOptions();

		mockMetrics
			.Setup(x => x.StartJobSubmitActivity(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>()))
			.Returns((Activity?)null);

		mockJobStore.Setup(store => store.GetJobById(job.Id, It.IsAny<CancellationToken>()))
			.ReturnsAsync(MethodResult<Job>.Failure(AsyncEndpointError.FromCode("JOB_NOT_FOUND", $"Job with ID {job.Id} not found")));
		mockJobStore.Setup(store => store.CreateJob(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(MethodResult.Success);

		var jobManager = new JobManager(
			mockJobStore.Object,
			mockLogger.Object,
			options,
			mockDateTimeProvider.Object,
			mockMetrics.Object);

		var headers = new Dictionary<string, List<string?>>();
		var routeParams = new Dictionary<string, object?>();
		var queryParams = new List<KeyValuePair<string, List<string?>>>();

		await jobManager.SubmitJob(jobName, payload, jobId, headers, routeParams, queryParams, CancellationToken.None);

		mockMetrics.Verify(m => m.RecordJobCreated(jobName, It.IsAny<string>()), Times.Once);
	}

	[Theory, AutoMoqData]
	public async Task ProcessJobFailure_MaxRetriesExceeded_RecordsJobFailedMetric(
		Guid jobId,
		Mock<IJobStore> mockJobStore,
		Mock<ILogger<JobManager>> mockLogger,
		Mock<TimeProvider> mockDateTimeProvider,
		Mock<IAsyncEndpointsObservability> mockMetrics,
		AsyncEndpointError error)
	{
		var options = new AsyncEndpointsOptions();

		var job = Job.Create(
			jobId,
			"TestJob",
			"{}",
			[],
			[],
			[],
			5,
			mockDateTimeProvider.Object);

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
			options,
			mockDateTimeProvider.Object,
			mockMetrics.Object);

		await jobManager.ProcessJobFailure(jobId, error, CancellationToken.None);

		mockMetrics.Verify(m => m.RecordJobFailed(job.Name, error.Code, It.IsAny<string>()), Times.Once);
	}

	[Theory, AutoMoqData]
	public async Task ProcessJobFailure_RetriesAvailable_RecordsRetryMetric(
		Guid jobId,
		Mock<IJobStore> mockJobStore,
		Mock<ILogger<JobManager>> mockLogger,
		Mock<TimeProvider> mockDateTimeProvider,
		Mock<IAsyncEndpointsObservability> mockMetrics,
		AsyncEndpointError error)
	{
		var options = new AsyncEndpointsOptions();

		var job = Job.Create(
			jobId,
			"TestJob",
			"{}",
			[],
			[],
			[],
			5,
			mockDateTimeProvider.Object);

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
			options,
			mockDateTimeProvider.Object,
			mockMetrics.Object);

		await jobManager.ProcessJobFailure(jobId, error, CancellationToken.None);

		mockMetrics.Verify(m => m.RecordJobRetries(job.Name, It.IsAny<string>()), Times.Once);
	}
}
