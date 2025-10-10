using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.Infrastructure.Serialization;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.Redis.Services;
using AsyncEndpoints.Redis.Storage;
using AsyncEndpoints.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace AsyncEndpoints.Redis.UnitTests.Storage;

public class RedisJobStoreExceptionTests
{
	/// <summary>
	/// Verifies that when Redis operations fail during job creation, the RedisJobStore properly handles the exception and returns an error result.
	/// This test ensures robust error handling when the underlying Redis service is unavailable.
	/// </summary>
	[Theory, AutoMoqData]
	public async Task CreateJob_WhenRedisOperationFails_ShouldReturnErrorResult(
		Mock<ILogger<RedisJobStore>> mockLogger,
		Mock<IDateTimeProvider> mockDateTimeProvider,
		Mock<IJobHashConverter> mockJobHashConverter,
		Mock<ISerializer> mockSerializer,
		Mock<IDatabase> mockDatabase,
		Job job)
	{
		// Arrange
		job.Name = "TestJob";

		var hashEntries = new[] { new HashEntry("Id", job.Id.ToString()) };
		mockJobHashConverter
			.Setup(x => x.ConvertToHashEntries(job))
			.Returns(hashEntries);

		mockDatabase
			.Setup(x => x.HashSetAsync(It.IsAny<RedisKey>(), It.IsAny<HashEntry[]>(), CommandFlags.None))
			.Throws(new RedisException("Redis operation failed"));

		var mockRedisLuaScriptService = new Mock<IRedisLuaScriptService>();
		var store = new RedisJobStore(mockLogger.Object, mockDatabase.Object, mockDateTimeProvider.Object, mockJobHashConverter.Object, mockSerializer.Object, mockRedisLuaScriptService.Object);

		// Act
		var result = await store.CreateJob(job, It.IsAny<CancellationToken>());

		// Assert
		Assert.False(result.IsSuccess);
		Assert.Contains("Unexpected error creating job", result.Error.Message);
		Assert.Contains("Redis operation failed", result.Error.Message);
	}

	/// <summary>
	/// Verifies that when Redis operations fail during job retrieval, the RedisJobStore properly handles the exception and returns an error result.
	/// This test ensures robust error handling when the underlying Redis service is unavailable.
	/// </summary>
	[Theory, AutoMoqData]
	public async Task GetJobById_WhenRedisOperationFails_ShouldReturnErrorResult(
		Mock<ILogger<RedisJobStore>> mockLogger,
		Mock<IDateTimeProvider> mockDateTimeProvider,
		Mock<IJobHashConverter> mockJobHashConverter,
		Mock<ISerializer> mockSerializer,
		Mock<IDatabase> mockDatabase,
		Guid jobId)
	{
		// Arrange
		mockDatabase
			.Setup(x => x.HashGetAllAsync(It.IsAny<RedisKey>(), CommandFlags.None))
			.ThrowsAsync(new RedisException("Redis operation failed"));

		var mockRedisLuaScriptService = new Mock<IRedisLuaScriptService>();
		var store = new RedisJobStore(mockLogger.Object, mockDatabase.Object, mockDateTimeProvider.Object, mockJobHashConverter.Object, mockSerializer.Object, mockRedisLuaScriptService.Object);

		// Act
		var result = await store.GetJobById(jobId, It.IsAny<CancellationToken>());

		// Assert
		Assert.False(result.IsSuccess);
		Assert.Contains("Unexpected error retrieving job", result.Error.Message);
		Assert.Contains("Redis operation failed", result.Error.Message);
	}

	/// <summary>
	/// Verifies that when Redis operations fail during job update, the RedisJobStore properly handles the exception and returns an error result.
	/// This test ensures robust error handling when the underlying Redis service is unavailable.
	/// </summary>
	[Theory, AutoMoqData]
	public async Task UpdateJob_WhenRedisOperationFails_ShouldReturnErrorResult(
		Mock<ILogger<RedisJobStore>> mockLogger,
		Mock<IDateTimeProvider> mockDateTimeProvider,
		Mock<IJobHashConverter> mockJobHashConverter,
		Mock<ISerializer> mockSerializer,
		Mock<IDatabase> mockDatabase,
		Job job)
	{
		// Arrange
		job.Name = "TestJob";

		var hashEntries = new[] { new HashEntry("Id", job.Id.ToString()) };
		mockJobHashConverter
			.Setup(x => x.ConvertToHashEntries(job))
			.Returns(hashEntries);

		mockDatabase
			.Setup(x => x.KeyExistsAsync(It.IsAny<RedisKey>(), CommandFlags.None))
			.ReturnsAsync(true);

		mockDatabase
			.Setup(x => x.HashSetAsync(It.IsAny<RedisKey>(), It.IsAny<HashEntry[]>(), CommandFlags.None))
			.ThrowsAsync(new RedisException("Redis operation failed"));

		var mockRedisLuaScriptService = new Mock<IRedisLuaScriptService>();
		var store = new RedisJobStore(mockLogger.Object, mockDatabase.Object, mockDateTimeProvider.Object, mockJobHashConverter.Object, mockSerializer.Object, mockRedisLuaScriptService.Object);

		// Act
		var result = await store.UpdateJob(job, It.IsAny<CancellationToken>());

		// Assert
		Assert.False(result.IsSuccess);
		Assert.Contains("Unexpected error updating job", result.Error.Message);
		Assert.Contains("Redis operation failed", result.Error.Message);
	}
}
