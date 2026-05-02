using AsyncEndpoints.Abstractions.Jobs;

namespace AsyncEndpoints.Abstractions.UnitTests.Jobs;

public class JobRecordTests
{
	[Fact]
	public void JobRecord_DefaultValues_AreCorrect()
	{
		// Act
		var record = new JobRecord();

		// Assert
		Assert.NotEqual(Guid.Empty, record.JobId);
		Assert.Equal("default", record.Channel);
		Assert.Equal(0, record.Priority);
		Assert.Equal(JobStatus.Queued, record.Status);
		Assert.Equal(0, record.RetryCount);
		Assert.Equal(3, record.MaxRetries);
		Assert.True((DateTimeOffset.UtcNow - record.CreatedAt).TotalSeconds < 1);
		Assert.Null(record.StartedAt);
		Assert.Null(record.CompletedAt);
		Assert.Null(record.WorkerId);
		Assert.Null(record.LastHeartbeat);
		Assert.Null(record.Result);
		Assert.Null(record.ErrorMessage);
	}
}
