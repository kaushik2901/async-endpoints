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
    public async Task CreateJob_WithValidJob_Succeeds()
    {
        // Arrange
        var job = new Job { Id = Guid.NewGuid(), Name = "TestJob" };

        // Act
        var result = await _jobStore.CreateJob(job, _cancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task CreateJob_WithNullJob_Fails()
    {
        // Act
        var result = await _jobStore.CreateJob(null!, _cancellationToken);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("INVALID_JOB", result.Error!.Code);
    }

    [Fact]
    public async Task CreateJob_WithEmptyJobId_Fails()
    {
        // Arrange
        var job = new Job { Id = Guid.Empty, Name = "TestJob" };

        // Act
        var result = await _jobStore.CreateJob(job, _cancellationToken);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("INVALID_JOB_ID", result.Error!.Code);
    }

    [Fact]
    public async Task CreateJob_WithDuplicateJobId_Fails()
    {
        // Arrange
        var job = new Job { Id = Guid.NewGuid(), Name = "TestJob" };

        // Act
        await _jobStore.CreateJob(job, _cancellationToken);
        var duplicateResult = await _jobStore.CreateJob(job, _cancellationToken);

        // Assert
        Assert.False(duplicateResult.IsSuccess);
        Assert.NotNull(duplicateResult.Error);
        Assert.Equal("JOB_CREATE_FAILED", duplicateResult.Error!.Code);
    }

    [Fact]
    public async Task GetJobById_WithValidId_ReturnsJob()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var job = new Job { Id = jobId, Name = "TestJob" };
        await _jobStore.CreateJob(job, _cancellationToken);

        // Act
        var result = await _jobStore.GetJobById(jobId, _cancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(jobId, result.Data!.Id);
        Assert.Equal("TestJob", result.Data!.Name);
    }

    [Fact]
    public async Task GetJobById_WithEmptyId_Fails()
    {
        // Act
        var result = await _jobStore.GetJobById(Guid.Empty, _cancellationToken);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("INVALID_JOB_ID", result.Error!.Code);
    }

    [Fact]
    public async Task GetJobById_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _jobStore.GetJobById(nonExistentId, _cancellationToken);

        // Assert
        Assert.True(result.IsSuccess); // Success means operation completed, but data is null
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task ClaimJobsForWorker_ReturnsAvailableJobs()
    {
        // Arrange
        var workerId = Guid.NewGuid();
        var job1 = new Job { Id = Guid.NewGuid(), Name = "Job1", Status = JobStatus.Queued };
        var job2 = new Job { Id = Guid.NewGuid(), Name = "Job2", Status = JobStatus.Scheduled, RetryDelayUntil = DateTime.UtcNow.AddMinutes(-1) };
        var job3 = new Job { Id = Guid.NewGuid(), Name = "Job3", Status = JobStatus.InProgress }; // Should not be returned

        await _jobStore.CreateJob(job1, _cancellationToken);
        await _jobStore.CreateJob(job2, _cancellationToken);
        await _jobStore.CreateJob(job3, _cancellationToken);

        // Act
        var result = await _jobStore.ClaimJobsForWorker(workerId, 10, _cancellationToken);

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
    public async Task ClaimJobsForWorker_WithScheduledJobsInFuture_DoesNotReturnFutureScheduledJobs()
    {
        // Arrange
        var workerId = Guid.NewGuid();
        var job1 = new Job { Id = Guid.NewGuid(), Name = "Job1", Status = JobStatus.Queued };
        var job2 = new Job { Id = Guid.NewGuid(), Name = "Job2", Status = JobStatus.Scheduled, RetryDelayUntil = DateTime.UtcNow.AddMinutes(10) }; // Future scheduled job

        await _jobStore.CreateJob(job1, _cancellationToken);
        await _jobStore.CreateJob(job2, _cancellationToken);

        // Act
        var result = await _jobStore.ClaimJobsForWorker(workerId, 10, _cancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Contains(job1.Id, result.Data.Select(j => j.Id));
        Assert.DoesNotContain(job2.Id, result.Data.Select(j => j.Id));
    }

    [Fact]
    public async Task ClaimJobsForWorker_WithMaxSize_RespectsLimit()
    {
        // Arrange
        var workerId = Guid.NewGuid();
        var jobs = new List<Job>();
        for (int i = 0; i < 5; i++)
        {
            var job = new Job { Id = Guid.NewGuid(), Name = $"Job{i}", Status = JobStatus.Queued };
            await _jobStore.CreateJob(job, _cancellationToken);
            jobs.Add(job);
        }

        // Act
        var result = await _jobStore.ClaimJobsForWorker(workerId, 3, _cancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data!.Count);
    }

    [Fact]
    public async Task UpdateJob_UpdatesCompleteJob()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var job = new Job { Id = jobId, Name = "TestJob", Status = JobStatus.Queued };
        await _jobStore.CreateJob(job, _cancellationToken);

        // Act
        job.Name = "Updated Job Name";
        job.Status = JobStatus.InProgress;
        var result = await _jobStore.UpdateJob(job, _cancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);

        var getJobResult = await _jobStore.GetJobById(jobId, _cancellationToken);
        Assert.Equal("Updated Job Name", getJobResult.Data!.Name);
        Assert.Equal(JobStatus.InProgress, getJobResult.Data!.Status);
    }

    [Fact]
    public async Task CreateJob_WithCancelledToken_ReturnsCancelledTask()
    {
        // Arrange
        var job = new Job { Id = Guid.NewGuid(), Name = "TestJob" };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => _jobStore.CreateJob(job, cts.Token));
    }

    [Fact]
    public async Task GetJobById_WithCancelledToken_ReturnsCancelledTask()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => _jobStore.GetJobById(Guid.NewGuid(), cts.Token));
    }

    [Fact]
    public async Task ClaimJobsForWorker_WithCancelledToken_ReturnsCancelledTask()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => _jobStore.ClaimJobsForWorker(Guid.NewGuid(), 10, cts.Token));
    }
}