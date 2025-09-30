using AsyncEndpoints.Contracts;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Services;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.UnitTests.Services;

public class JobResponseServiceTests
{
    private readonly Mock<ILogger<JobResponseService>> _mockLogger;
    private readonly Mock<IJobStore> _mockJobStore;
    private readonly JobResponseService _service;
    private readonly CancellationToken _cancellationToken;

    public JobResponseServiceTests()
    {
        _mockLogger = new Mock<ILogger<JobResponseService>>();
        _mockJobStore = new Mock<IJobStore>();
        _service = new JobResponseService(_mockLogger.Object, _mockJobStore.Object);
        _cancellationToken = CancellationToken.None;
    }

    [Fact]
    public async Task GetJobResponseAsync_WhenJobExists_ReturnsOkResultWithJobResponse()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var expectedJob = new Job
        {
            Id = jobId,
            Name = "Test Job",
            Status = JobStatus.Completed,
            Result = "Test Result",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var getJobResult = MethodResult<Job>.Success(expectedJob);
        _mockJobStore.Setup(x => x.GetJobById(jobId, _cancellationToken))
            .ReturnsAsync(getJobResult);

        // Act
        var result = await _service.GetJobResponseAsync(jobId, _cancellationToken);

        // Assert
        Assert.NotNull(result);

        // Check that result is not null (basic verification without execution)
        // The actual HTTP result type can be tested using reflection or specific type checking
        Assert.Contains("Ok", result.GetType().Name);
    }

    [Fact]
    public async Task GetJobResponseAsync_WhenJobDoesNotExist_ReturnsNotFoundResult()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        var getJobResult = MethodResult<Job>.Failure("Job not found");
        _mockJobStore.Setup(x => x.GetJobById(jobId, _cancellationToken))
            .ReturnsAsync(getJobResult);

        // Act
        var result = await _service.GetJobResponseAsync(jobId, _cancellationToken);

        // Assert
        Assert.NotNull(result);

        // Check that result is not null (basic verification without execution)
        Assert.Contains("NotFound", result.GetType().Name);
    }

    [Fact]
    public async Task GetJobResponseAsync_WhenJobStoreReturnsFailure_ReturnsNotFoundResult()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var getJobResult = MethodResult<Job>.Failure("Job not found");
        _mockJobStore.Setup(x => x.GetJobById(jobId, _cancellationToken))
            .ReturnsAsync(getJobResult);

        // Act
        var result = await _service.GetJobResponseAsync(jobId, _cancellationToken);

        // Assert
        Assert.NotNull(result);

        // Check that result is not null (basic verification without execution)
        Assert.Contains("NotFound", result.GetType().Name);
    }

    [Fact]
    public async Task GetJobResponseAsync_WhenJobExists_InvokesLoggerCorrectly()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var expectedJob = new Job
        {
            Id = jobId,
            Name = "Test Job",
            Status = JobStatus.Completed
        };

        var getJobResult = MethodResult<Job>.Success(expectedJob);
        _mockJobStore.Setup(x => x.GetJobById(jobId, _cancellationToken))
            .ReturnsAsync(getJobResult);

        // Act
        var result = await _service.GetJobResponseAsync(jobId, _cancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Retrieving job response for job ID: {jobId}")),
                It.IsAny<Exception?>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Successfully retrieved job response for job ID: {jobId}")),
                It.IsAny<Exception?>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task GetJobResponseAsync_WhenJobDoesNotExist_InvokesLoggerCorrectly()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        var getJobResult = MethodResult<Job>.Failure("Job not found");
        _mockJobStore.Setup(x => x.GetJobById(jobId, _cancellationToken))
            .ReturnsAsync(getJobResult);

        // Act
        var result = await _service.GetJobResponseAsync(jobId, _cancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Retrieving job response for job ID: {jobId}")),
                It.IsAny<Exception?>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Job with ID {jobId} not found")),
                It.IsAny<Exception?>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Successfully retrieved job response for job ID: {jobId}")),
                It.IsAny<Exception?>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Never);
    }
}