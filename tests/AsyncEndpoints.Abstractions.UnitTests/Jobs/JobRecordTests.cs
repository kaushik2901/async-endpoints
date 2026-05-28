using AsyncEndpoints.Abstractions.Jobs;

namespace AsyncEndpoints.Abstractions.UnitTests.Jobs;

public class JobRecordTests
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		var record = new JobRecord();

		Assert.Equal(Guid.Empty, record.JobId);
		Assert.Equal(string.Empty, record.JobName);
		Assert.Equal("default", record.Channel);
		Assert.Equal(0, record.Priority);
		Assert.Null(record.Partition);
		Assert.Equal(string.Empty, record.Payload);
		Assert.Equal(default(JobStatus), record.Status);
		Assert.Equal(0, record.RetryCount);
		Assert.Equal(0, record.MaxRetries);
		Assert.Equal(default, record.CreatedAt);
		Assert.Null(record.StartedAt);
		Assert.Null(record.CompletedAt);
		Assert.Null(record.WorkerId);
		Assert.Null(record.LastHeartbeat);
		Assert.Null(record.Result);
		Assert.Null(record.ErrorMessage);
		Assert.Null(record.Metadata);
	}

	[Fact]
	public void InitProperties_CanBeSet()
	{
		var now = DateTime.UtcNow;
		var metadata = new Dictionary<string, string> { ["key"] = "val" };

		var record = new JobRecord
		{
			JobId = Guid.NewGuid(),
			JobName = "test",
			Channel = "critical",
			Priority = 5,
			Partition = 1,
			Payload = "{}",
			Status = JobStatus.Queued,
			RetryCount = 2,
			MaxRetries = 5,
			CreatedAt = now,
			StartedAt = now,
			CompletedAt = null,
			WorkerId = "worker-1",
			LastHeartbeat = now,
			Result = "done",
			ErrorMessage = null,
			Metadata = metadata
		};

		Assert.NotEqual(Guid.Empty, record.JobId);
		Assert.Equal("test", record.JobName);
		Assert.Equal(5, record.Priority);
		Assert.Equal(1, record.Partition);
		Assert.Equal(JobStatus.Queued, record.Status);
		Assert.Equal(2, record.RetryCount);
		Assert.Equal(5, record.MaxRetries);
		Assert.Equal(now, record.CreatedAt);
		Assert.Equal("worker-1", record.WorkerId);
		Assert.Equal("done", record.Result);
		Assert.Same(metadata, record.Metadata);
	}
}
