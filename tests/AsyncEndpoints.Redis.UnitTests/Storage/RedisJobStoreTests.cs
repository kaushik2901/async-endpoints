using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.Infrastructure.Serialization;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.Redis.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace AsyncEndpoints.Redis.UnitTests;

public class RedisJobStoreTests
{
	private readonly Mock<IDatabase> _mockDatabase;
	private readonly Mock<ILogger<RedisJobStore>> _mockLogger;
	private readonly Mock<ISerializer> _mockSerializer;
	private readonly Mock<IJobHashConverter> _mockJobHashConverter;
	private readonly RedisJobStore _redisJobStore;

	public RedisJobStoreTests()
	{
		_mockDatabase = new Mock<IDatabase>();
		_mockLogger = new Mock<ILogger<RedisJobStore>>();
		_mockSerializer = new Mock<ISerializer>();
		_mockJobHashConverter = new Mock<IJobHashConverter>();
		var mockDateTimeProvider = new Mock<IDateTimeProvider>();
		_redisJobStore = new RedisJobStore(_mockLogger.Object, _mockDatabase.Object, mockDateTimeProvider.Object, _mockJobHashConverter.Object, _mockSerializer.Object);
	}

	[Fact]
	public async Task CreateJob_ValidJob_ReturnsSuccess()
	{
		// Arrange
		var job = new Job { Id = Guid.NewGuid(), Name = "TestJob", Status = JobStatus.Queued, Payload = "{}" };
		_mockDatabase.Setup(db => db.KeyExistsAsync($"ae:job:{job.Id}", It.IsAny<CommandFlags>()))
					 .ReturnsAsync(false);
		_mockDatabase.Setup(db => db.SortedSetAddAsync("ae:jobs:queue", job.Id.ToString(),
					 It.IsAny<double>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
					 .ReturnsAsync(true);

		// Act
		var result = await _redisJobStore.CreateJob(job, default);

		// Assert
		Assert.True(result.IsSuccess);
		_mockDatabase.Verify(db => db.HashSetAsync($"ae:job:{job.Id}", It.IsAny<HashEntry[]>(), CommandFlags.None), Times.Once);
		_mockJobHashConverter.Verify(x => x.ConvertToHashEntries(job), Times.Once);
	}

	[Fact]
	public async Task CreateJob_NullJob_ReturnsFailure()
	{
		// Act
		var result = await _redisJobStore.CreateJob(default!, default);

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
		var hashEntries = new[] { new HashEntry("Id", jobId.ToString()), new HashEntry("Name", "TestJob") };
		_mockDatabase.Setup(db => db.HashGetAllAsync($"ae:job:{jobId}", It.IsAny<CommandFlags>()))
					 .ReturnsAsync(hashEntries);
		_mockJobHashConverter.Setup(x => x.ConvertFromHashEntries(hashEntries)).Returns(job);

		// Act
		var result = await _redisJobStore.GetJobById(jobId, default);

		// Assert
		Assert.True(result.IsSuccess);
		Assert.Equal(jobId, result.Data.Id);
		Assert.Equal("TestJob", result.Data.Name);
		_mockJobHashConverter.Verify(x => x.ConvertFromHashEntries(hashEntries), Times.Once);
	}

	[Fact]
	public async Task GetJobById_NonExistingJob_ReturnsFailure()
	{
		// Arrange
		var jobId = Guid.NewGuid();
		_mockDatabase.Setup(db => db.HashGetAllAsync($"ae:job:{jobId}", It.IsAny<CommandFlags>()))
					 .ReturnsAsync(Array.Empty<HashEntry>());

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
		var hashEntries = new[] { new HashEntry("Id", job.Id.ToString()) };
		var mockDateTimeProvider = new Mock<IDateTimeProvider>();
		var now = DateTimeOffset.UtcNow;
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(now);

		// Create a new instance of RedisJobStore with the mock DateTimeProvider
		var redisJobStore = new RedisJobStore(_mockLogger.Object, _mockDatabase.Object, mockDateTimeProvider.Object, _mockJobHashConverter.Object, _mockSerializer.Object);

		_mockDatabase.Setup(db => db.KeyExistsAsync($"ae:job:{job.Id}", It.IsAny<CommandFlags>()))
					 .ReturnsAsync(true);
		_mockJobHashConverter.Setup(x => x.ConvertToHashEntries(It.IsAny<Job>())).Returns(hashEntries);
		_mockDatabase.Setup(db => db.SortedSetRemoveAsync("ae:jobs:queue", job.Id.ToString(), It.IsAny<CommandFlags>()))
					 .ReturnsAsync(true);

		// Act
		var result = await redisJobStore.UpdateJob(job, default);

		// Assert
		Assert.True(result.IsSuccess);
		_mockDatabase.Verify(db => db.HashSetAsync($"ae:job:{job.Id}", hashEntries, CommandFlags.None), Times.Once);
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
}
