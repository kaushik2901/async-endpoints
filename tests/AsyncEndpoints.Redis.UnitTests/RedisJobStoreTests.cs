using AsyncEndpoints.Entities;
using AsyncEndpoints.Redis;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace AsyncEndpoints.Redis.UnitTests;

public class RedisJobStoreTests
{
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<ILogger<RedisJobStore>> _mockLogger;
    private readonly RedisJobStore _redisJobStore;

    public RedisJobStoreTests()
    {
        _mockDatabase = new Mock<IDatabase>();
        _mockLogger = new Mock<ILogger<RedisJobStore>>();
        _redisJobStore = new RedisJobStore(_mockLogger.Object, _mockDatabase.Object);
    }

    [Fact]
    public async Task CreateJob_ValidJob_ReturnsSuccess()
    {
        // Arrange
        var job = new Job { Id = Guid.NewGuid(), Name = "TestJob", Payload = "{}" };
        _mockDatabase.Setup(db => db.StringGetAsync($"ae:job:{job.Id}", It.IsAny<CommandFlags>()))
                     .ReturnsAsync(RedisValue.Null);
        _mockDatabase.Setup(db => db.StringSetAsync($"ae:job:{job.Id}", It.IsAny<RedisValue>(), 
                     It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                     .ReturnsAsync(true);
        _mockDatabase.Setup(db => db.SortedSetAddAsync("ae:jobs:queue", job.Id.ToString(), 
                     It.IsAny<double>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                     .ReturnsAsync(true);

        // Act
        var result = await _redisJobStore.CreateJob(job, default);

        // Assert
        Assert.True(result.IsSuccess);
        _mockDatabase.Verify(db => db.StringSetAsync($"ae:job:{job.Id}", It.IsAny<RedisValue>(), 
                     It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task CreateJob_NullJob_ReturnsFailure()
    {
        // Act
        var result = await _redisJobStore.CreateJob(null, default);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Job cannot be null", result.Error.Message);
    }

    [Fact]
    public async Task CreateJob_EmptyGuidJob_ReturnsFailure()
    {
        // Arrange
        var job = new Job { Id = Guid.Empty };

        // Act
        var result = await _redisJobStore.CreateJob(job, default);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Job ID cannot be empty", result.Error.Message);
    }

    [Fact]
    public async Task GetJobById_ExistingJob_ReturnsJob()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var job = new Job { Id = jobId, Name = "TestJob" };
        var jobJson = System.Text.Json.JsonSerializer.Serialize(job);
        _mockDatabase.Setup(db => db.StringGetAsync($"ae:job:{jobId}", It.IsAny<CommandFlags>()))
                     .ReturnsAsync(jobJson);

        // Act
        var result = await _redisJobStore.GetJobById(jobId, default);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(jobId, result.Data.Id);
        Assert.Equal("TestJob", result.Data.Name);
    }

    [Fact]
    public async Task GetJobById_NonExistingJob_ReturnsFailure()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _mockDatabase.Setup(db => db.StringGetAsync($"ae:job:{jobId}", It.IsAny<CommandFlags>()))
                     .ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _redisJobStore.GetJobById(jobId, default);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error.Message);
    }

    [Fact]
    public async Task UpdateJob_ExistingJob_ReturnsSuccess()
    {
        // Arrange
        var job = new Job { Id = Guid.NewGuid(), Name = "TestJob", Payload = "{}" };
        _mockDatabase.Setup(db => db.KeyExistsAsync($"ae:job:{job.Id}", It.IsAny<CommandFlags>()))
                     .ReturnsAsync(true);
        _mockDatabase.Setup(db => db.StringSetAsync($"ae:job:{job.Id}", It.IsAny<RedisValue>(), 
                     null, It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                     .ReturnsAsync(true);
        _mockDatabase.Setup(db => db.SortedSetRemoveAsync("ae:jobs:queue", job.Id.ToString(), It.IsAny<CommandFlags>()))
                     .ReturnsAsync(true);

        // Act
        var result = await _redisJobStore.UpdateJob(job, default);

        // Assert
        Assert.True(result.IsSuccess);
        _mockDatabase.Verify(db => db.StringSetAsync($"ae:job:{job.Id}", It.IsAny<RedisValue>(), 
                     null, It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task UpdateJob_NonExistingJob_ReturnsFailure()
    {
        // Arrange
        var job = new Job { Id = Guid.NewGuid() };
        _mockDatabase.Setup(db => db.KeyExistsAsync($"ae:job:{job.Id}", It.IsAny<CommandFlags>()))
                     .ReturnsAsync(false);

        // Act
        var result = await _redisJobStore.UpdateJob(job, default);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error.Message);
    }

    [Fact]
    public async Task UpdateJob_NullJob_ReturnsFailure()
    {
        // Act
        var result = await _redisJobStore.UpdateJob(null, default);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Job cannot be null", result.Error.Message);
    }
}