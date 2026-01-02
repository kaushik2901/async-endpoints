using AsyncEndpoints.Infrastructure.Serialization;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.Redis.Services;
using AsyncEndpoints.Utilities;
using Moq;

namespace AsyncEndpoints.Redis.UnitTests.Services;

public class JobHashConverterTests
{
	/// <summary>
	/// Verifies that the JobHashConverter correctly converts a job instance to Redis hash entries.
	/// This test ensures all job properties are properly serialized to hash entries for Redis storage.
	/// </summary>
	[Fact]
	public void ConvertToHashEntries_WithValidJob_ReturnsCorrectHashEntries()
	{
		// Arrange
		var mockDateTimeProvider = new Mock<AsyncEndpoints.Infrastructure.IDateTimeProvider>();
		var currentTime = DateTimeOffset.UtcNow;
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(currentTime);
		var jobId = Guid.NewGuid();
		var workerId = Guid.NewGuid();
		var job = Job.Create(
			jobId,
			"TestJob",
			"{}",
			new Dictionary<string, List<string?>> { { "Content-Type", new List<string?> { "application/json" } } },
			new Dictionary<string, object?> { { "id", "123" } },
			[new("page", ["1"])],
			3, // MaxRetries = 3
			mockDateTimeProvider.Object);

		// Manually set the properties that were set in the original test
		job = job.CreateCopy(
			status: JobStatus.InProgress,
			result: "Success",
			retryCount: 1,
			retryDelayUntil: DateTime.UtcNow.AddMinutes(5),
			workerId: workerId,
			startedAt: DateTimeOffset.UtcNow.AddSeconds(1),
			completedAt: DateTimeOffset.UtcNow.AddSeconds(2),
			lastUpdatedAt: DateTimeOffset.UtcNow.AddSeconds(3),
			error: AsyncEndpointError.FromMessage("Error message"),
			dateTimeProvider: mockDateTimeProvider.Object);

		var mockSerializer = new Mock<ISerializer>();
		mockSerializer.Setup(s => s.Serialize(It.IsAny<object>(), null)).Returns("serialized_value");
		mockSerializer.Setup(s => s.Deserialize<Dictionary<string, List<string?>>>("serialized_value", null))
			.Returns(new Dictionary<string, List<string?>> { { "Content-Type", new List<string?> { "application/json" } } });
		mockSerializer.Setup(s => s.Deserialize<Dictionary<string, object?>>("serialized_value", null))
			.Returns(new Dictionary<string, object?> { { "id", "123" } });
		mockSerializer.Setup(s => s.Deserialize<List<KeyValuePair<string, List<string?>>>>("serialized_value", null))
			.Returns([new("page", ["1"])]);
		mockSerializer.Setup(s => s.Deserialize<AsyncEndpointError>("serialized_value", null))
			.Returns(AsyncEndpointError.FromMessage("Error message"));

		var converter = new JobHashConverter(mockSerializer.Object);

		// Act
		var hashEntries = converter.ConvertToHashEntries(job);

		// Assert
		Assert.Contains(hashEntries, entry => entry.Name == "Id" && entry.Value == job.Id.ToString());
		Assert.Contains(hashEntries, entry => entry.Name == "Name" && entry.Value == job.Name);
		Assert.Contains(hashEntries, entry => entry.Name == "Status" && entry.Value == ((int)job.Status).ToString());
		Assert.Contains(hashEntries, entry => entry.Name == "Payload" && entry.Value == job.Payload);
		Assert.Contains(hashEntries, entry => entry.Name == "Result" && entry.Value == job.Result);
		Assert.Contains(hashEntries, entry => entry.Name == "RetryCount" && entry.Value == job.RetryCount.ToString());
		Assert.Contains(hashEntries, entry => entry.Name == "MaxRetries" && entry.Value == job.MaxRetries.ToString());
		Assert.Contains(hashEntries, entry => entry.Name == "WorkerId" && entry.Value == job.WorkerId.ToString());
	}

	/// <summary>
	/// Verifies that the JobHashConverter correctly converts Redis hash entries back to a job instance.
	/// This test ensures all job properties are properly deserialized from hash entries retrieved from Redis.
	/// </summary>
	[Fact]
	public void ConvertFromHashEntries_WithValidHashEntries_ReturnsCorrectJob()
	{
		// Arrange
		var jobId = Guid.NewGuid();
		var workerId = Guid.NewGuid();
		var createdAt = DateTimeOffset.UtcNow;
		var hashEntries = new[]
		{
			new StackExchange.Redis.HashEntry("Id", jobId.ToString()),
			new StackExchange.Redis.HashEntry("Name", "TestJob"),
			new StackExchange.Redis.HashEntry("Status", ((int)JobStatus.Completed).ToString()),
			new StackExchange.Redis.HashEntry("Payload", "{}"),
			new StackExchange.Redis.HashEntry("Headers", "[]"),
			new StackExchange.Redis.HashEntry("QueryParams", "[]"),
			new StackExchange.Redis.HashEntry("RouteParams", "[]"),
			new StackExchange.Redis.HashEntry("Result", "ResultData"),
			new StackExchange.Redis.HashEntry("Error", ""),
			new StackExchange.Redis.HashEntry("RetryCount", "2"),
			new StackExchange.Redis.HashEntry("RetryDelayUntil", ""),
			new StackExchange.Redis.HashEntry("MaxRetries", "5"),
			new StackExchange.Redis.HashEntry("StartedAt", ""),
			new StackExchange.Redis.HashEntry("CompletedAt", ""),
			new StackExchange.Redis.HashEntry("WorkerId", workerId.ToString()),
			new StackExchange.Redis.HashEntry("CreatedAt", createdAt.ToString("O")),
			new StackExchange.Redis.HashEntry("LastUpdatedAt", createdAt.AddSeconds(1).ToString("O"))
		};

		var mockSerializer = new Mock<ISerializer>();
		mockSerializer.Setup(s => s.Deserialize<Dictionary<string, List<string?>>>(It.IsAny<string>(), null))
			.Returns([]);
		mockSerializer.Setup(s => s.Deserialize<Dictionary<string, object?>>(It.IsAny<string>(), null))
			.Returns([]);
		mockSerializer.Setup(s => s.Deserialize<List<KeyValuePair<string, List<string?>>>>(It.IsAny<string>(), null))
			.Returns([]);
		mockSerializer.Setup(s => s.Deserialize<AsyncEndpointError>(It.IsAny<string>(), null))
			.Returns(AsyncEndpointError.FromMessage("Test error"));

		var converter = new JobHashConverter(mockSerializer.Object);

		// Act
		var job = converter.ConvertFromHashEntries(hashEntries);

		// Assert
		Assert.Equal(jobId, job.Id);
		Assert.Equal("TestJob", job.Name);
		Assert.Equal(JobStatus.Completed, job.Status);
		Assert.Equal("{}", job.Payload);
		Assert.Empty(job.Headers);
		Assert.Empty(job.QueryParams);
		Assert.Empty(job.RouteParams);
		Assert.Equal("ResultData", job.Result);
		Assert.Null(job.Error);
		Assert.Equal(2, job.RetryCount);
		Assert.Null(job.RetryDelayUntil);
		Assert.Equal(5, job.MaxRetries);
		Assert.Null(job.StartedAt);
		Assert.Null(job.CompletedAt);
		Assert.Equal(workerId, job.WorkerId);
		Assert.Equal(createdAt, job.CreatedAt);
	}
}
