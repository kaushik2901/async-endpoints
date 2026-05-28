using AsyncEndpoints.Abstractions.Infrastructure;
using AsyncEndpoints.Abstractions.Utilities;
using AsyncEndpoints.Core.Legacy.JobProcessing;
using Moq;

namespace AsyncEndpoints.Core.UnitTests.JobProcessing;

public class JobTests
{
	[Fact]
	public void Create_WithAllParameters_SetsPropertiesCorrectly()
	{
		var id = Guid.NewGuid();
		var name = "TestJob";
		var payload = "{\"data\":\"value\"}";
		var headers = new Dictionary<string, List<string?>> { { "header1", new List<string?> { "value1" } } };
		var routeParams = new Dictionary<string, object?> { { "param1", "value1" } };
		var queryParams = new List<KeyValuePair<string, List<string?>>> { new("query1", ["value1"]) };
		var maxRetries = 2;
		var mockDateTimeProvider = new Mock<IDateTimeProvider>();
		var expectedTime = DateTimeOffset.UtcNow;
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);

		var job = Job.Create(id, name, payload, headers, routeParams, queryParams, maxRetries, mockDateTimeProvider.Object);

		Assert.Equal(id, job.Id);
		Assert.Equal(name, job.Name);
		Assert.Equal(payload, job.Payload);
		Assert.Equal(headers, job.Headers);
		Assert.Equal(routeParams, job.RouteParams);
		Assert.Equal(queryParams, job.QueryParams);
		Assert.Equal(JobStatus.Queued, job.Status);
		Assert.Equal(0, job.RetryCount);
		Assert.Equal(maxRetries, job.MaxRetries);
		Assert.Equal(expectedTime, job.CreatedAt);
		Assert.Equal(expectedTime, job.LastUpdatedAt);
	}

	[Fact]
	public void UpdateStatus_UpdatesStatusAndLastUpdatedAt()
	{
		var mockDateTimeProvider = new Mock<IDateTimeProvider>();
		var newTime = DateTimeOffset.UtcNow.AddSeconds(1);
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(newTime);
		var job = Job.Create(Guid.NewGuid(), "TestJob", "{\"data\":\"value\"}", [], [], [], 2, mockDateTimeProvider.Object);

		job.UpdateStatus(JobStatus.InProgress, mockDateTimeProvider.Object);

		Assert.Equal(JobStatus.InProgress, job.Status);
		Assert.Equal(newTime, job.LastUpdatedAt);
		Assert.Equal(newTime, job.StartedAt);
	}

	[Fact]
	public void UpdateStatus_WithCompletedStatus_SetsCompletedAt()
	{
		var mockDateTimeProvider = new Mock<IDateTimeProvider>();
		var expectedTime = DateTimeOffset.UtcNow;
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);
		var job = Job.Create(Guid.NewGuid(), "TestJob", "{\"data\":\"value\"}", [], [], [], 2, mockDateTimeProvider.Object);

		job.UpdateStatus(JobStatus.Completed, mockDateTimeProvider.Object);

		Assert.Equal(JobStatus.Completed, job.Status);
		Assert.Equal(expectedTime, job.CompletedAt);
	}

	[Fact]
	public void UpdateStatus_WithFailedStatus_SetsCompletedAt()
	{
		var mockDateTimeProvider = new Mock<IDateTimeProvider>();
		var expectedTime = DateTimeOffset.UtcNow;
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);
		var job = Job.Create(Guid.NewGuid(), "TestJob", "{\"data\":\"value\"}", [], [], [], 2, mockDateTimeProvider.Object);

		job.UpdateStatus(JobStatus.Failed, mockDateTimeProvider.Object);

		Assert.Equal(JobStatus.Failed, job.Status);
		Assert.Equal(expectedTime, job.CompletedAt);
	}

	[Fact]
	public void UpdateStatus_WithCanceledStatus_SetsCompletedAt()
	{
		var mockDateTimeProvider = new Mock<IDateTimeProvider>();
		var expectedTime = DateTimeOffset.UtcNow;
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);
		var job = Job.Create(Guid.NewGuid(), "TestJob", "{\"data\":\"value\"}", [], [], [], 2, mockDateTimeProvider.Object);

		job.UpdateStatus(JobStatus.Canceled, mockDateTimeProvider.Object);

		Assert.Equal(JobStatus.Canceled, job.Status);
		Assert.Equal(expectedTime, job.CompletedAt);
	}

	[Fact]
	public void SetResult_UpdatesStatusAndResult()
	{
		var mockDateTimeProvider = new Mock<IDateTimeProvider>();
		var expectedTime = DateTimeOffset.UtcNow;
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);
		var job = Job.Create(Guid.NewGuid(), "TestJob", "{\"data\":\"value\"}", [], [], [], 2, mockDateTimeProvider.Object);
		var result = "Success";

		job.SetResult(result, mockDateTimeProvider.Object);

		Assert.Equal(result, job.Result);
		Assert.Equal(JobStatus.Completed, job.Status);
		Assert.Equal(expectedTime, job.CompletedAt);
	}

	[Fact]
	public void IncrementRetryCount_IncrementsRetryCount()
	{
		var currentTime = DateTimeOffset.UtcNow;
		var job = new Job(currentTime);
		var initialRetryCount = job.RetryCount;

		job.IncrementRetryCount();

		Assert.Equal(initialRetryCount + 1, job.RetryCount);
	}

	[Fact]
	public void SetRetryTime_SetsRetryDelayUntil()
	{
		var currentTime = DateTimeOffset.UtcNow;
		var job = new Job(currentTime);
		var retryTime = DateTime.UtcNow.AddMinutes(5);

		job.SetRetryTime(retryTime);

		Assert.Equal(retryTime, job.RetryDelayUntil);
	}

	[Fact]
	public void DefaultValues_AreCorrect()
	{
		var currentTime = DateTimeOffset.UtcNow;

		var job = new Job(currentTime);

		Assert.NotEqual(Guid.Empty, job.Id);
		Assert.Equal(string.Empty, job.Name);
		Assert.Equal(JobStatus.Queued, job.Status);
		Assert.Equal(string.Empty, job.Payload);
		Assert.Null(job.Result);
		Assert.Null(job.Error);
		Assert.Equal(0, job.RetryCount);
		Assert.Equal(3, job.MaxRetries);
		Assert.Equal(currentTime, job.CreatedAt);
		Assert.Equal(currentTime, job.LastUpdatedAt);
		Assert.Null(job.RetryDelayUntil);
		Assert.Null(job.WorkerId);
		Assert.Null(job.StartedAt);
		Assert.Null(job.CompletedAt);
		Assert.NotNull(job.Headers);
		Assert.NotNull(job.RouteParams);
		Assert.NotNull(job.QueryParams);
	}

	[Fact]
	public void UpdateStatus_WithValidStateTransition_Succeeds()
	{
		var mockDateTimeProvider = new Mock<IDateTimeProvider>();
		var expectedTime = DateTimeOffset.UtcNow;
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);
		var job = Job.Create(Guid.NewGuid(), "TestJob", "{\"data\":\"value\"}", [], [], [], 2, mockDateTimeProvider.Object);

		job.UpdateStatus(JobStatus.InProgress, mockDateTimeProvider.Object);
		Assert.Equal(JobStatus.InProgress, job.Status);

		job.UpdateStatus(JobStatus.Completed, mockDateTimeProvider.Object);
		Assert.Equal(JobStatus.Completed, job.Status);
	}

	[Fact]
	public void UpdateStatus_WithValidRetryTransition_Succeeds()
	{
		var mockDateTimeProvider = new Mock<IDateTimeProvider>();
		var expectedTime = DateTimeOffset.UtcNow;
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);
		var job = Job.Create(Guid.NewGuid(), "TestJob", "{\"data\":\"value\"}", [], [], [], 2, mockDateTimeProvider.Object);
		job.UpdateStatus(JobStatus.Failed, mockDateTimeProvider.Object);

		job.UpdateStatus(JobStatus.Queued, mockDateTimeProvider.Object);
		Assert.Equal(JobStatus.Queued, job.Status);
	}

	[Fact]
	public void UpdateStatus_WithInvalidStateTransition_ThrowsInvalidOperationException()
	{
		var mockDateTimeProvider = new Mock<IDateTimeProvider>();
		var expectedTime = DateTimeOffset.UtcNow;
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);
		var job = Job.Create(Guid.NewGuid(), "TestJob", "{\"data\":\"value\"}", [], [], [], 2, mockDateTimeProvider.Object);
		job.UpdateStatus(JobStatus.Completed, mockDateTimeProvider.Object);

		var exception = Assert.Throws<InvalidOperationException>(() =>
			job.UpdateStatus(JobStatus.InProgress, mockDateTimeProvider.Object));
		Assert.Contains("Invalid state transition", exception.Message);
	}

	[Fact]
	public void UpdateStatus_WithSameState_DoesNotThrow()
	{
		var mockDateTimeProvider = new Mock<IDateTimeProvider>();
		var expectedTime = DateTimeOffset.UtcNow;
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(expectedTime);
		var job = Job.Create(Guid.NewGuid(), "TestJob", "{\"data\":\"value\"}", [], [], [], 2, mockDateTimeProvider.Object);

		job.UpdateStatus(JobStatus.Queued, mockDateTimeProvider.Object);
		Assert.Equal(JobStatus.Queued, job.Status);
	}

	[Fact]
	public void CreateCopy_CreatesNewInstanceWithSameProperties()
	{
		var mockDateTimeProvider = new Mock<IDateTimeProvider>();
		var createdTime = DateTimeOffset.UtcNow;
		var startedTime = DateTimeOffset.UtcNow.AddMinutes(1);
		var completedTime = DateTimeOffset.UtcNow.AddMinutes(2);
		var lastUpdatedTime = DateTimeOffset.UtcNow.AddMinutes(3);

		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(lastUpdatedTime);

		var id = Guid.NewGuid();
		var job = new Job(createdTime)
		{
			Id = id,
			Name = "TestJob",
			Status = JobStatus.InProgress,
			Headers = new Dictionary<string, List<string?>> { { "header1", new List<string?> { "value1" } } },
			RouteParams = new Dictionary<string, object?> { { "param1", "value1" } },
			QueryParams = [new("query1", ["value1"])],
			Payload = "{\"data\":\"value\"}",
			Result = "Success",
			Error = new AsyncEndpointError("TEST_ERROR", "Test error", null),
			RetryCount = 2,
			MaxRetries = 5,
			RetryDelayUntil = DateTime.UtcNow.AddMinutes(10),
			WorkerId = Guid.NewGuid(),
			CreatedAt = createdTime,
			StartedAt = startedTime,
			CompletedAt = completedTime,
			LastUpdatedAt = createdTime
		};

		var copiedJob = job.CreateCopy();

		Assert.NotSame(job, copiedJob);
		Assert.Equal(job.Id, copiedJob.Id);
		Assert.Equal(job.Name, copiedJob.Name);
		Assert.Equal(job.Status, copiedJob.Status);
		Assert.Equal(job.Payload, copiedJob.Payload);
		Assert.Equal(job.Result, copiedJob.Result);
		Assert.Equal(job.Error?.Message, copiedJob.Error?.Message);
		Assert.Equal(job.RetryCount, copiedJob.RetryCount);
		Assert.Equal(job.MaxRetries, copiedJob.MaxRetries);
		Assert.Equal(job.RetryDelayUntil, copiedJob.RetryDelayUntil);
		Assert.Equal(job.WorkerId, copiedJob.WorkerId);
		Assert.Equal(job.CreatedAt, copiedJob.CreatedAt);
		Assert.Equal(job.StartedAt, copiedJob.StartedAt);
		Assert.Equal(job.CompletedAt, copiedJob.CompletedAt);
		Assert.Equal(job.LastUpdatedAt, copiedJob.LastUpdatedAt);

		Assert.NotSame(job.Headers, copiedJob.Headers);
		Assert.Equal(job.Headers, copiedJob.Headers);

		Assert.NotSame(job.RouteParams, copiedJob.RouteParams);
		Assert.Equal(job.RouteParams, copiedJob.RouteParams);

		Assert.NotSame(job.QueryParams, copiedJob.QueryParams);
		Assert.Equal(job.QueryParams.Count, copiedJob.QueryParams.Count);
		for (int i = 0; i < job.QueryParams.Count; i++)
		{
			Assert.Equal(job.QueryParams[i].Key, copiedJob.QueryParams[i].Key);
			Assert.Equal(job.QueryParams[i].Value, copiedJob.QueryParams[i].Value);
			Assert.NotSame(job.QueryParams[i].Value, copiedJob.QueryParams[i].Value);
		}
	}

	[Fact]
	public void CreateCopy_UpdatesSpecifiedPropertiesOnly()
	{
		var mockDateTimeProvider = new Mock<IDateTimeProvider>();
		var createdTime = DateTimeOffset.UtcNow;
		var currentTime = DateTimeOffset.UtcNow.AddMinutes(5);
		var startedTime = DateTimeOffset.UtcNow.AddMinutes(1);
		var completedTime = DateTimeOffset.UtcNow.AddMinutes(2);

		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(currentTime);

		var id = Guid.NewGuid();
		var job = new Job(createdTime)
		{
			Id = id,
			Name = "OriginalJob",
			Status = JobStatus.Queued,
			Payload = "{\"data\":\"original\"}",
			Result = "OriginalResult",
			RetryCount = 1,
			WorkerId = null,
			CreatedAt = createdTime,
			StartedAt = startedTime,
			CompletedAt = completedTime,
			LastUpdatedAt = createdTime
		};

		var copiedJob = job.CreateCopy(
			status: JobStatus.InProgress,
			workerId: Guid.NewGuid(),
			result: "NewResult",
			retryCount: 3,
			lastUpdatedAt: currentTime
		);

		Assert.NotSame(job, copiedJob);
		Assert.Equal(job.Id, copiedJob.Id);
		Assert.Equal(job.Name, copiedJob.Name);
		Assert.Equal(job.Payload, copiedJob.Payload);
		Assert.Equal(job.CreatedAt, copiedJob.CreatedAt);
		Assert.Equal(job.StartedAt, copiedJob.StartedAt);
		Assert.Equal(job.CompletedAt, copiedJob.CompletedAt);

		Assert.Equal(JobStatus.InProgress, copiedJob.Status);
		Assert.NotNull(copiedJob.WorkerId);
		Assert.Equal("NewResult", copiedJob.Result);
		Assert.Equal(3, copiedJob.RetryCount);
		Assert.Equal(currentTime, copiedJob.LastUpdatedAt);
	}

	[Fact]
	public void CreateCopy_UsesDateTimeProviderForLastUpdatedTime()
	{
		var mockDateTimeProvider = new Mock<IDateTimeProvider>();
		var createdTime = DateTimeOffset.UtcNow;
		var newTime = DateTimeOffset.UtcNow.AddMinutes(10);

		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(newTime);

		var job = new Job(createdTime)
		{
			Name = "TestJob",
			Status = JobStatus.Queued
		};

		var copiedJob = job.CreateCopy(dateTimeProvider: mockDateTimeProvider.Object);

		Assert.Equal(newTime, copiedJob.LastUpdatedAt);
	}

	[Fact]
	public void CreateCopy_UpdatesStartedAtWhenInProgress()
	{
		var mockDateTimeProvider = new Mock<IDateTimeProvider>();
		var createdTime = DateTimeOffset.UtcNow;
		var newTime = DateTimeOffset.UtcNow.AddMinutes(10);

		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(newTime);

		var job = new Job(createdTime)
		{
			Name = "TestJob",
			Status = JobStatus.Queued
		};

		var copiedJob = job.CreateCopy(
			status: JobStatus.InProgress,
			startedAt: newTime
		);

		Assert.Equal(JobStatus.InProgress, copiedJob.Status);
		Assert.Equal(newTime, copiedJob.StartedAt);
	}
}
