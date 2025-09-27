using AsyncEndpoints.Contracts;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Services;
using AsyncEndpoints.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AutoFixture.Xunit2;
using AutoFixture;
using Moq;
using AsyncEndpoints.UnitTests.TestSupport;

namespace AsyncEndpoints.UnitTests.Services;

public class JobManagerTests
{
    [Theory, AutoMoqData]
    public void Constructor_Succeeds_WithValidDependencies(
        Mock<IJobStore> mockJobStore,
        Mock<ILogger<JobManager>> mockLogger)
    {
        // Arrange
        var options = Options.Create(new AsyncEndpointsConfigurations());

        // Act
        var manager = new JobManager(mockJobStore.Object, mockLogger.Object, options);

        // Assert
        Assert.NotNull(manager);
    }

    [Theory, AutoMoqData]
    public async Task SubmitJob_CreatesNewJob_WhenJobDoesNotExist(
        [Frozen] Mock<IJobStore> mockJobStore,
        [Frozen] Mock<ILogger<JobManager>> mockLogger,
        string jobName,
        string payload,
        Job newJob)
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var options = Options.Create(new AsyncEndpointsConfigurations());
        
        mockJobStore
            .Setup(x => x.GetJobById(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MethodResult<Job>.Failure("Job not found"));
        mockJobStore
            .Setup(x => x.CreateJob(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MethodResult<Job>.Success(newJob));

        var jobManager = new JobManager(mockJobStore.Object, mockLogger.Object, options);

        // Act
        var result = await jobManager.SubmitJob(jobName, payload, httpContext, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        mockJobStore.Verify(x => x.CreateJob(It.IsAny<Job>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory, AutoMoqData]
    public async Task SubmitJob_ReturnsExistingJob_WhenJobAlreadyExists(
        [Frozen] Mock<IJobStore> mockJobStore,
        [Frozen] Mock<ILogger<JobManager>> mockLogger,
        string jobName,
        string payload,
        Job existingJob)
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        var options = Options.Create(new AsyncEndpointsConfigurations());
        
        httpContext.Request.Headers[AsyncEndpointsConstants.JobIdHeaderName] = jobId.ToString();
        
        mockJobStore
            .Setup(x => x.GetJobById(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MethodResult<Job>.Success(existingJob));

        var jobManager = new JobManager(mockJobStore.Object, mockLogger.Object, options);

        // Act
        var result = await jobManager.SubmitJob(jobName, payload, httpContext, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Same(existingJob, result.Data);
        mockJobStore.Verify(x => x.CreateJob(It.IsAny<Job>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory, AutoMoqData]
    public async Task ClaimJobsForProcessing_ReturnsJobs_WhenJobsAvailable(
        [Frozen] Mock<IJobStore> mockJobStore,
        [Frozen] Mock<ILogger<JobManager>> mockLogger,
        Guid workerId,
        int maxClaimCount,
        List<Job> jobs)
    {
        // Arrange
        var options = Options.Create(new AsyncEndpointsConfigurations());
        
        mockJobStore
            .Setup(x => x.ClaimJobsForWorker(workerId, maxClaimCount, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MethodResult<List<Job>>.Success(jobs));

        var jobManager = new JobManager(mockJobStore.Object, mockLogger.Object, options);

        // Act
        var result = await jobManager.ClaimJobsForProcessing(workerId, maxClaimCount, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Same(jobs, result.Data);
    }

    [Theory, AutoMoqData]
    public async Task ProcessJobSuccess_UpdatesJobWithResult_WhenJobExists(
        [Frozen] Mock<IJobStore> mockJobStore,
        [Frozen] Mock<ILogger<JobManager>> mockLogger,
        Guid jobId,
        string resultData)
    {
        // Arrange
        var options = Options.Create(new AsyncEndpointsConfigurations());
        var job = new Fixture().Create<Job>();
        
        mockJobStore
            .Setup(x => x.GetJobById(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MethodResult<Job>.Success(job));
        mockJobStore
            .Setup(x => x.UpdateJob(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MethodResult.Success());

        var jobManager = new JobManager(mockJobStore.Object, mockLogger.Object, options);

        // Act
        var result = await jobManager.ProcessJobSuccess(jobId, resultData, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal(resultData, job.Result);
    }

    [Theory, AutoMoqData]
    public async Task ProcessJobSuccess_ReturnsFailure_WhenJobDoesNotExist(
        [Frozen] Mock<IJobStore> mockJobStore,
        [Frozen] Mock<ILogger<JobManager>> mockLogger,
        Guid jobId,
        string resultData)
    {
        // Arrange
        var options = Options.Create(new AsyncEndpointsConfigurations());
        
        mockJobStore
            .Setup(x => x.GetJobById(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MethodResult<Job>.Failure("Job not found"));

        var jobManager = new JobManager(mockJobStore.Object, mockLogger.Object, options);

        // Act
        var result = await jobManager.ProcessJobSuccess(jobId, resultData, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }

    [Theory, AutoMoqData]
    public async Task ProcessJobFailure_SetsException_WhenMaxRetriesReached(
        [Frozen] Mock<IJobStore> mockJobStore,
        [Frozen] Mock<ILogger<JobManager>> mockLogger,
        Guid jobId,
        string exception)
    {
        // Arrange
        var options = Options.Create(new AsyncEndpointsConfigurations());
        var job = new Fixture().Create<Job>();
        
        job.MaxRetries = 0; // Force max retries to be reached
        mockJobStore
            .Setup(x => x.GetJobById(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MethodResult<Job>.Success(job));
        mockJobStore
            .Setup(x => x.UpdateJob(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MethodResult.Success());

        var jobManager = new JobManager(mockJobStore.Object, mockLogger.Object, options);

        // Act
        var result = await jobManager.ProcessJobFailure(jobId, exception, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal(exception, job.Exception);
    }

    [Theory, AutoMoqData]
    public async Task ProcessJobFailure_SchedulesRetry_WhenRetriesAvailable(
        [Frozen] Mock<IJobStore> mockJobStore,
        [Frozen] Mock<ILogger<JobManager>> mockLogger,
        Guid jobId,
        string exception)
    {
        // Arrange
        var options = Options.Create(new AsyncEndpointsConfigurations());
        var job = new Fixture().Create<Job>();
        
        job.MaxRetries = 3;
        job.RetryCount = 0;
        mockJobStore
            .Setup(x => x.GetJobById(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MethodResult<Job>.Success(job));
        mockJobStore
            .Setup(x => x.UpdateJob(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MethodResult.Success());

        var jobManager = new JobManager(mockJobStore.Object, mockLogger.Object, options);

        // Act
        var result = await jobManager.ProcessJobFailure(jobId, exception, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(JobStatus.Scheduled, job.Status);
        Assert.Equal(1, job.RetryCount);
        Assert.Equal(exception, job.Exception);
    }
}