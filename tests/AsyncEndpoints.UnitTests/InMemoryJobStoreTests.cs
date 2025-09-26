using AsyncEndpoints.Entities;
using AsyncEndpoints.InMemoryStore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.UnitTests;

public class InMemoryJobStoreTests
{
    private readonly Mock<ILogger<InMemoryJobStore>> _mockLogger;
    private readonly InMemoryJobStore _jobStore;
    private readonly CancellationToken _cancellationToken;

    public InMemoryJobStoreTests()
    {
        _mockLogger = new Mock<ILogger<InMemoryJobStore>>();
        _jobStore = new InMemoryJobStore(_mockLogger.Object);
        _cancellationToken = CancellationToken.None;
    }

    [Fact]
    public async Task Add_WithValidJob_Succeeds()
    {
        // Arrange
        var job = new Job { Id = Guid.NewGuid(), Name = "TestJob" };

        // Act
        var result = await _jobStore.Add(job, _cancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Add_WithNullJob_Fails()
    {
        // Act
        var result = await _jobStore.Add(null!, _cancellationToken);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("INVALID_JOB", result.Error!.Code);
    }

    [Fact]
    public async Task Add_WithEmptyJobId_Fails()
    {
        // Arrange
        var job = new Job { Id = Guid.Empty, Name = "TestJob" };

        // Act
        var result = await _jobStore.Add(job, _cancellationToken);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("INVALID_JOB_ID", result.Error!.Code);
    }

    [Fact]
    public async Task Add_WithDuplicateJobId_Fails()
    {
        // Arrange
        var job = new Job { Id = Guid.NewGuid(), Name = "TestJob" };
        await _jobStore.Add(job, _cancellationToken);

        // Act
        var duplicateResult = await _jobStore.Add(job, _cancellationToken);

        // Assert
        Assert.False(duplicateResult.IsSuccess);
        Assert.NotNull(duplicateResult.Error);
        Assert.Equal("JOB_ADD_FAILED", duplicateResult.Error!.Code);
    }

    [Fact]
    public async Task Get_WithValidId_ReturnsJob()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var job = new Job { Id = jobId, Name = "TestJob" };
        await _jobStore.Add(job, _cancellationToken);

        // Act
        var result = await _jobStore.Get(jobId, _cancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(jobId, result.Data!.Id);
        Assert.Equal("TestJob", result.Data.Name);
    }

    [Fact]
    public async Task Get_WithEmptyId_Fails()
    {
        // Act
        var result = await _jobStore.Get(Guid.Empty, _cancellationToken);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("INVALID_JOB_ID", result.Error!.Code);
    }

    [Fact]
    public async Task Get_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _jobStore.Get(nonExistentId, _cancellationToken);

        // Assert
        Assert.True(result.IsSuccess); // Success means operation completed, but data is null
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetQueuedJobs_ReturnsAvailableJobs()
    {
        // Arrange
        var workerId = Guid.NewGuid();
        var job1 = new Job { Id = Guid.NewGuid(), Name = "Job1", Status = JobStatus.Queued };
        var job2 = new Job { Id = Guid.NewGuid(), Name = "Job2", Status = JobStatus.Scheduled, RetryDelayUntil = DateTime.UtcNow.AddMinutes(-1) };
        var job3 = new Job { Id = Guid.NewGuid(), Name = "Job3", Status = JobStatus.InProgress }; // Should not be returned

        await _jobStore.Add(job1, _cancellationToken);
        await _jobStore.Add(job2, _cancellationToken);
        await _jobStore.Add(job3, _cancellationToken);

        // Act
        var result = await _jobStore.GetQueuedJobs(workerId, 10, _cancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data!.Count);

        var jobIds = result.Data.Select(j => j.Id).ToList();
        Assert.Contains(job1.Id, jobIds);
        Assert.Contains(job2.Id, jobIds);

        // Check that worker ID was assigned to jobs
        foreach (var job in result.Data)
        {
            Assert.Equal(workerId, job.WorkerId);
        }
    }

    [Fact]
    public async Task GetQueuedJobs_WithScheduledJobsInFuture_DoesNotReturnFutureScheduledJobs()
    {
        // Arrange
        var workerId = Guid.NewGuid();
        var job1 = new Job { Id = Guid.NewGuid(), Name = "Job1", Status = JobStatus.Queued };
        var job2 = new Job { Id = Guid.NewGuid(), Name = "Job2", Status = JobStatus.Scheduled, RetryDelayUntil = DateTime.UtcNow.AddMinutes(10) }; // Future scheduled job

        await _jobStore.Add(job1, _cancellationToken);
        await _jobStore.Add(job2, _cancellationToken);

        // Act
        var result = await _jobStore.GetQueuedJobs(workerId, 10, _cancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data); // Only job1 should be returned
        Assert.Contains(job1.Id, result.Data.Select(j => j.Id));
        Assert.DoesNotContain(job2.Id, result.Data.Select(j => j.Id));
    }

    [Fact]
    public async Task GetQueuedJobs_WithMaxSize_RespectsLimit()
    {
        // Arrange
        var workerId = Guid.NewGuid();
        var jobs = new List<Job>();
        for (int i = 0; i < 5; i++)
        {
            var job = new Job { Id = Guid.NewGuid(), Name = $"Job{i}", Status = JobStatus.Queued };
            await _jobStore.Add(job, _cancellationToken);
            jobs.Add(job);
        }

        // Act
        var result = await _jobStore.GetQueuedJobs(workerId, 3, _cancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data!.Count);
    }

    [Fact]
    public async Task UpdateJobStatus_WithValidJob_UpdatesStatus()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var job = new Job { Id = jobId, Name = "TestJob", Status = JobStatus.Queued };
        await _jobStore.Add(job, _cancellationToken);

        // Act
        var result = await _jobStore.UpdateJobStatus(jobId, JobStatus.InProgress, _cancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);

        var getJobResult = await _jobStore.Get(jobId, _cancellationToken);
        Assert.Equal(JobStatus.InProgress, getJobResult.Data!.Status);
    }

    [Fact]
    public async Task UpdateJobStatus_WithEmptyId_Fails()
    {
        // Act
        var result = await _jobStore.UpdateJobStatus(Guid.Empty, JobStatus.Completed, _cancellationToken);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("INVALID_JOB_ID", result.Error!.Code);
    }

    [Fact]
    public async Task UpdateJobStatus_WithNonExistentJob_Fails()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _jobStore.UpdateJobStatus(nonExistentId, JobStatus.Completed, _cancellationToken);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("JOB_NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task UpdateJobResult_WithValidJob_UpdatesResult()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var job = new Job { Id = jobId, Name = "TestJob", Status = JobStatus.Queued };
        await _jobStore.Add(job, _cancellationToken);
        var expectedResult = "Job completed successfully";

        // Act
        var result = await _jobStore.UpdateJobResult(jobId, expectedResult, _cancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);

        var getJobResult = await _jobStore.Get(jobId, _cancellationToken);
        Assert.Equal(expectedResult, getJobResult.Data!.Result);
        Assert.Equal(JobStatus.Completed, getJobResult.Data.Status);
    }

    [Fact]
    public async Task UpdateJobResult_WithEmptyId_Fails()
    {
        // Act
        var result = await _jobStore.UpdateJobResult(Guid.Empty, "result", _cancellationToken);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("INVALID_JOB_ID", result.Error!.Code);
    }

    [Fact]
    public async Task UpdateJobResult_WithNonExistentJob_Fails()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _jobStore.UpdateJobResult(nonExistentId, "result", _cancellationToken);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("JOB_NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task UpdateJobException_WithValidJob_UpdatesException()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var job = new Job { Id = jobId, Name = "TestJob", Status = JobStatus.Queued, RetryCount = 3, MaxRetries = 3 }; // At max retries already
        await _jobStore.Add(job, _cancellationToken);
        var expectedException = "An error occurred";

        // Act
        var result = await _jobStore.UpdateJobException(jobId, expectedException, _cancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);

        var getJobResult = await _jobStore.Get(jobId, _cancellationToken);
        Assert.Equal(expectedException, getJobResult.Data!.Exception);
        Assert.Equal(JobStatus.Failed, getJobResult.Data.Status); // Should be failed since at max retries
    }

    [Fact]
    public async Task UpdateJobException_WithRetryLogic_WhenRetriesLeft_SchedulesRetry()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var job = new Job { Id = jobId, Name = "TestJob", Status = JobStatus.Queued, MaxRetries = 3 };
        await _jobStore.Add(job, _cancellationToken);
        var expectedException = "An error occurred";

        // Act
        var result = await _jobStore.UpdateJobException(jobId, expectedException, _cancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);

        var getJobResult = await _jobStore.Get(jobId, _cancellationToken);
        Assert.Equal(expectedException, getJobResult.Data!.Exception);
        Assert.Equal(1, getJobResult.Data.RetryCount); // First retry
        Assert.Equal(JobStatus.Scheduled, getJobResult.Data.Status); // Should be scheduled for retry
        Assert.Null(getJobResult.Data.WorkerId); // Worker should be reset
    }

    [Fact]
    public async Task UpdateJobException_WhenMaxRetriesReached_MarksAsFailed()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var job = new Job { Id = jobId, Name = "TestJob", Status = JobStatus.Queued, RetryCount = 3, MaxRetries = 3 };
        await _jobStore.Add(job, _cancellationToken);
        var expectedException = "An error occurred";

        // Act
        var result = await _jobStore.UpdateJobException(jobId, expectedException, _cancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);

        var getJobResult = await _jobStore.Get(jobId, _cancellationToken);
        Assert.Equal(expectedException, getJobResult.Data!.Exception);
        Assert.Equal(3, getJobResult.Data.RetryCount); // No increment
        Assert.Equal(JobStatus.Failed, getJobResult.Data.Status); // Should be marked as failed
    }

    [Fact]
    public async Task UpdateJobException_WithEmptyId_Fails()
    {
        // Act
        var result = await _jobStore.UpdateJobException(Guid.Empty, "exception", _cancellationToken);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("INVALID_JOB_ID", result.Error!.Code);
    }

    [Fact]
    public async Task UpdateJobException_WithNonExistentJob_Fails()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _jobStore.UpdateJobException(nonExistentId, "exception", _cancellationToken);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("JOB_NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task Add_WithCancelledToken_ReturnsCancelledTask()
    {
        // Arrange
        var job = new Job { Id = Guid.NewGuid(), Name = "TestJob" };
        var cancellationToken = new CancellationToken(true); // Cancelled token

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => _jobStore.Add(job, cancellationToken));
    }

    [Fact]
    public async Task Get_WithCancelledToken_ReturnsCancelledTask()
    {
        // Arrange
        var cancellationToken = new CancellationToken(true); // Cancelled token

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => _jobStore.Get(Guid.NewGuid(), cancellationToken));
    }

    [Fact]
    public async Task GetQueuedJobs_WithCancelledToken_ReturnsCancelledTask()
    {
        // Arrange
        var cancellationToken = new CancellationToken(true); // Cancelled token

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => _jobStore.GetQueuedJobs(Guid.NewGuid(), 10, cancellationToken));
    }

    [Fact]
    public async Task UpdateJobStatus_WithCancelledToken_ReturnsCancelledTask()
    {
        // Arrange
        var cancellationToken = new CancellationToken(true); // Cancelled token

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => _jobStore.UpdateJobStatus(Guid.NewGuid(), JobStatus.Completed, cancellationToken));
    }

    [Fact]
    public async Task UpdateJobResult_WithCancelledToken_ReturnsCancelledTask()
    {
        // Arrange
        var cancellationToken = new CancellationToken(true); // Cancelled token

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => _jobStore.UpdateJobResult(Guid.NewGuid(), "result", cancellationToken));
    }

    [Fact]
    public async Task UpdateJobException_WithCancelledToken_ReturnsCancelledTask()
    {
        // Arrange
        var cancellationToken = new CancellationToken(true); // Cancelled token

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => _jobStore.UpdateJobException(Guid.NewGuid(), "exception", cancellationToken));
    }
}