using AsyncEndpoints.Entities;
using AsyncEndpoints.UnitTests.TestSupport;
using AsyncEndpoints.Utilities;

namespace AsyncEndpoints.UnitTests.Utilities;

public class JobResponseMapperTests
{
	[Theory, AutoMoqData]
	public void ToResponse_ReturnsCorrectJobResponse(
		Job job)
	{
		// Act
		var result = JobResponseMapper.ToResponse(job);

		// Assert
		Assert.NotNull(result);
		Assert.Equal(job.Id, result.Id);
		Assert.Equal(job.Name, result.Name);
		Assert.Equal(job.Status.ToString(), result.Status);
		Assert.Equal(job.Result, result.Result);
		Assert.Equal(job.Exception, result.Exception);
		Assert.Equal(job.RetryCount, result.RetryCount);
		Assert.Equal(job.MaxRetries, result.MaxRetries);
		Assert.Equal(job.CreatedAt, result.CreatedAt);
		Assert.Equal(job.StartedAt, result.StartedAt);
		Assert.Equal(job.CompletedAt, result.CompletedAt);
		Assert.Equal(job.LastUpdatedAt, result.LastUpdatedAt);
	}

	[Theory, AutoMoqData]
	public void ToResponse_HandlesNullValues(
		Job job)
	{
		// Arrange
		job.Result = null;
		job.Exception = null;
		job.StartedAt = null;
		job.CompletedAt = null;

		// Act
		var result = JobResponseMapper.ToResponse(job);

		// Assert
		Assert.NotNull(result);
		Assert.Equal(job.Id, result.Id);
		Assert.Equal(job.Name, result.Name);
		Assert.Equal(string.Empty, result.Result);
		Assert.Equal(string.Empty, result.Exception);
	}
}