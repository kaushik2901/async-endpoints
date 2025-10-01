using AsyncEndpoints.Contracts;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Serialization;
using AsyncEndpoints.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace AsyncEndpoints.Redis.UnitTests;

public class RedisJobStoreExceptionTests
{
	[Theory, AutoMoqData]
	public async Task CreateJob_WhenRedisOperationFails_ShouldReturnErrorResult(
		Mock<ILogger<RedisJobStore>> mockLogger,
		Mock<IDateTimeProvider> mockDateTimeProvider,
		Mock<ISerializer> mockSerializer,
		Mock<IDatabase> mockDatabase,
		Job job)
	{
		// Arrange
		job.Name = "TestJob";

		mockDatabase
			.Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), null, false, When.NotExists, CommandFlags.None))
			.ThrowsAsync(new RedisException("Redis operation failed"));

		mockSerializer
			.Setup(x => x.Serialize(job, null))
			.Returns("{}");

		var store = new RedisJobStore(mockLogger.Object, mockDatabase.Object, mockDateTimeProvider.Object, mockSerializer.Object);

		// Act
		var result = await store.CreateJob(job, It.IsAny<CancellationToken>());

		// Assert
		Assert.False(result.IsSuccess);
		Assert.Contains("Unexpected error creating job", result.Error.Message);
		Assert.Contains("Redis operation failed", result.Error.Message);
	}

	[Theory, AutoMoqData]
	public async Task GetJobById_WhenRedisOperationFails_ShouldReturnErrorResult(
		Mock<ILogger<RedisJobStore>> mockLogger,
		Mock<IDateTimeProvider> mockDateTimeProvider,
		Mock<ISerializer> mockSerializer,
		Mock<IDatabase> mockDatabase,
		Guid jobId)
	{
		// Arrange
		mockDatabase
			.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), CommandFlags.None))
			.ThrowsAsync(new RedisException("Redis operation failed"));

		var store = new RedisJobStore(mockLogger.Object, mockDatabase.Object, mockDateTimeProvider.Object, mockSerializer.Object);

		// Act
		var result = await store.GetJobById(jobId, It.IsAny<CancellationToken>());

		// Assert
		Assert.False(result.IsSuccess);
		Assert.Contains("Unexpected error retrieving job", result.Error.Message);
		Assert.Contains("Redis operation failed", result.Error.Message);
	}

	[Theory, AutoMoqData]
	public async Task UpdateJob_WhenRedisOperationFails_ShouldReturnErrorResult(
		Mock<ILogger<RedisJobStore>> mockLogger,
		Mock<IDateTimeProvider> mockDateTimeProvider,
		Mock<ISerializer> mockSerializer,
		Mock<IDatabase> mockDatabase,
		Job job)
	{
		// Arrange
		job.Name = "TestJob";

		mockDatabase
			.Setup(x => x.KeyExistsAsync(It.IsAny<RedisKey>(), CommandFlags.None))
			.ReturnsAsync(true);

		mockDatabase
			.Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), null, false, When.Always, CommandFlags.None))
			.ThrowsAsync(new RedisException("Redis operation failed"));

		mockSerializer
			.Setup(x => x.Serialize(job, null))
			.Returns("{}");

		var store = new RedisJobStore(mockLogger.Object, mockDatabase.Object, mockDateTimeProvider.Object, mockSerializer.Object);

		// Act
		var result = await store.UpdateJob(job, It.IsAny<CancellationToken>());

		// Assert
		Assert.False(result.IsSuccess);
		Assert.Contains("Unexpected error updating job", result.Error.Message);
		Assert.Contains("Redis operation failed", result.Error.Message);
	}
}
