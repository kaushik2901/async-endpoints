using AsyncEndpoints.Abstractions.Infrastructure;
using AsyncEndpoints.Abstractions.Utilities;
using AsyncEndpoints.Core.Configuration;
using AsyncEndpoints.Core.Legacy.JobProcessing;
using AsyncEndpoints.Core.Legacy.Observability;
using AsyncEndpoints.Core.UnitTests.TestSupport;
using AutoFixture.Xunit2;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.Core.UnitTests.JobProcessing;

public class JobManagerTests
{
	[Theory, AutoMoqData]
	public void Constructor_Succeeds_WithValidDependencies(
		Mock<IJobStore> mockJobStore,
		Mock<ILogger<JobManager>> mockLogger,
		Mock<IDateTimeProvider> mockDateTimeProvider,
		Mock<IAsyncEndpointsObservability> mockMetrics)
	{
		var options = new AsyncEndpointsOptions();

		var manager = new JobManager(mockJobStore.Object, mockLogger.Object, options, mockDateTimeProvider.Object, mockMetrics.Object);

		Assert.NotNull(manager);
	}

	[Theory, AutoMoqData]
	public async Task SubmitJob_CreatesNewJob_WhenJobDoesNotExist(
		[Frozen] Mock<IJobStore> mockJobStore,
		[Frozen] Mock<ILogger<JobManager>> mockLogger,
		[Frozen] Mock<IDateTimeProvider> mockDateTimeProvider,
		string jobName,
		string payload,
		Job newJob)
	{
		var options = new AsyncEndpointsOptions();

		mockJobStore
			.Setup(x => x.GetJobById(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(MethodResult<Job>.Failure("Job not found"));
		mockJobStore
			.Setup(x => x.CreateJob(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(MethodResult<Job>.Success(newJob));

		var jobManager = new JobManager(mockJobStore.Object, mockLogger.Object, options, mockDateTimeProvider.Object, Mock.Of<IAsyncEndpointsObservability>());

		var jobId = Guid.NewGuid();
		var headers = new Dictionary<string, List<string?>>();
		var routeParams = new Dictionary<string, object?>();
		var queryParams = new List<KeyValuePair<string, List<string?>>>();

		var result = await jobManager.SubmitJob(jobName, payload, jobId, headers, routeParams, queryParams, CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Data);
		mockJobStore.Verify(x => x.CreateJob(It.IsAny<Job>(), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Theory, AutoMoqData]
	public async Task SubmitJob_ReturnsExistingJob_WhenJobAlreadyExists(
		[Frozen] Mock<IJobStore> mockJobStore,
		[Frozen] Mock<ILogger<JobManager>> mockLogger,
		[Frozen] Mock<IDateTimeProvider> mockDateTimeProvider,
		string jobName,
		string payload,
		Job existingJob)
	{
		var jobId = Guid.NewGuid();
		var options = new AsyncEndpointsOptions();

		mockJobStore
			.Setup(x => x.GetJobById(jobId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(MethodResult<Job>.Success(existingJob));

		var jobManager = new JobManager(mockJobStore.Object, mockLogger.Object, options, mockDateTimeProvider.Object, Mock.Of<IAsyncEndpointsObservability>());

		var headers = new Dictionary<string, List<string?>>();
		var routeParams = new Dictionary<string, object?>();
		var queryParams = new List<KeyValuePair<string, List<string?>>>();

		var result = await jobManager.SubmitJob(jobName, payload, jobId, headers, routeParams, queryParams, CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Same(existingJob, result.Data);
		mockJobStore.Verify(x => x.CreateJob(It.IsAny<Job>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Theory, AutoMoqData]
	public async Task ClaimNextAvailableJob_ReturnsJob_WhenJobAvailable(
		[Frozen] Mock<IJobStore> mockJobStore,
		[Frozen] Mock<ILogger<JobManager>> mockLogger,
		[Frozen] Mock<IDateTimeProvider> mockDateTimeProvider,
		Guid workerId,
		Job job)
	{
		var options = new AsyncEndpointsOptions();

		mockJobStore
			.Setup(x => x.ClaimNextJobForWorker(workerId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(MethodResult<Job>.Success(job));

		var jobManager = new JobManager(mockJobStore.Object, mockLogger.Object, options, mockDateTimeProvider.Object, Mock.Of<IAsyncEndpointsObservability>());

		var result = await jobManager.ClaimNextAvailableJob(workerId, CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Same(job, result.Data);
	}

	[Theory, AutoMoqData]
	public async Task ProcessJobSuccess_UpdatesJobWithResult_WhenJobExists(
		[Frozen] Mock<IJobStore> mockJobStore,
		[Frozen] Mock<ILogger<JobManager>> mockLogger,
		[Frozen] Mock<IDateTimeProvider> mockDateTimeProvider,
		Guid jobId,
		string resultData,
		Job job)
	{
		var options = new AsyncEndpointsOptions();

		mockJobStore
			.Setup(x => x.GetJobById(jobId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(MethodResult<Job>.Success(job));
		mockJobStore
			.Setup(x => x.UpdateJob(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(MethodResult.Success());

		var jobManager = new JobManager(mockJobStore.Object, mockLogger.Object, options, mockDateTimeProvider.Object, Mock.Of<IAsyncEndpointsObservability>());

		var result = await jobManager.ProcessJobSuccess(jobId, resultData, CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal(JobStatus.Completed, job.Status);
		Assert.Equal(resultData, job.Result);
	}

	[Theory, AutoMoqData]
	public async Task ProcessJobSuccess_ReturnsFailure_WhenJobDoesNotExist(
		[Frozen] Mock<IJobStore> mockJobStore,
		[Frozen] Mock<ILogger<JobManager>> mockLogger,
		[Frozen] Mock<IDateTimeProvider> mockDateTimeProvider,
		Guid jobId,
		string resultData)
	{
		var options = new AsyncEndpointsOptions();

		mockJobStore
			.Setup(x => x.GetJobById(jobId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(MethodResult<Job>.Failure("Job not found"));

		var jobManager = new JobManager(mockJobStore.Object, mockLogger.Object, options, mockDateTimeProvider.Object, Mock.Of<IAsyncEndpointsObservability>());

		var result = await jobManager.ProcessJobSuccess(jobId, resultData, CancellationToken.None);

		Assert.False(result.IsSuccess);
	}

	[Theory, AutoMoqData]
	public async Task ProcessJobFailure_SetsError_WhenMaxRetriesReached(
		[Frozen] Mock<IJobStore> mockJobStore,
		[Frozen] Mock<ILogger<JobManager>> mockLogger,
		[Frozen] Mock<IDateTimeProvider> mockDateTimeProvider,
		Guid jobId,
		string error,
		Job job)
	{
		var options = new AsyncEndpointsOptions();

		job.MaxRetries = 0;
		mockJobStore
			.Setup(x => x.GetJobById(jobId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(MethodResult<Job>.Success(job));
		mockJobStore
			.Setup(x => x.UpdateJob(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(MethodResult.Success());

		var jobManager = new JobManager(mockJobStore.Object, mockLogger.Object, options, mockDateTimeProvider.Object, Mock.Of<IAsyncEndpointsObservability>());

		var result = await jobManager.ProcessJobFailure(jobId, AsyncEndpointError.FromMessage(error), CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal(JobStatus.Failed, job.Status);
		Assert.Equal(error, job.Error?.Message);
	}

	[Theory, AutoMoqData]
	public async Task ProcessJobFailure_SchedulesRetry_WhenRetriesAvailable(
		[Frozen] Mock<IJobStore> mockJobStore,
		[Frozen] Mock<ILogger<JobManager>> mockLogger,
		[Frozen] Mock<IDateTimeProvider> mockDateTimeProvider,
		Guid jobId,
		string error,
		Job job)
	{
		var options = new AsyncEndpointsOptions();

		job.MaxRetries = 3;
		job.RetryCount = 0;
		mockJobStore
			.Setup(x => x.GetJobById(jobId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(MethodResult<Job>.Success(job));
		mockJobStore
			.Setup(x => x.UpdateJob(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(MethodResult.Success());

		var jobManager = new JobManager(mockJobStore.Object, mockLogger.Object, options, mockDateTimeProvider.Object, Mock.Of<IAsyncEndpointsObservability>());

		var result = await jobManager.ProcessJobFailure(jobId, AsyncEndpointError.FromMessage(error), CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal(JobStatus.Scheduled, job.Status);
		Assert.Equal(1, job.RetryCount);
		Assert.Equal(error, job.Error?.Message);
	}
}
