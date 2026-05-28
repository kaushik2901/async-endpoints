using AsyncEndpoints.AspNetCore.Models;
using AsyncEndpoints.AspNetCore.UnitTests.TestSupport;
using AsyncEndpoints.Core.Legacy.JobProcessing;

namespace AsyncEndpoints.AspNetCore.UnitTests.Utilities;

public class JobResponseMapperTests
{
	[Theory, AutoMoqData]
	public void ToResponse_ReturnsCorrectJobResponse(
		Job job)
	{
		var result = JobResponseMapper.ToResponse(job);

		Assert.NotNull(result);
		Assert.Equal(job.Id, result.Id);
		Assert.Equal(job.Name, result.Name);
		Assert.Equal(job.Status.ToString(), result.Status);
		Assert.Equal(job.Result, result.Result);
		Assert.Equal(job.Error, result.Error);
		Assert.Equal(job.RetryCount, result.RetryCount);
		Assert.Equal(job.MaxRetries, result.MaxRetries);
		Assert.Equal(job.CreatedAt, result.CreatedAt);
		Assert.Equal(job.StartedAt, result.StartedAt);
		Assert.Equal(job.CompletedAt, result.CompletedAt);
		Assert.Equal(job.LastUpdatedAt, result.LastUpdatedAt);
	}

	[Theory, AutoMoqData]
	public void ToResponse_HandlesNullValues(Job job)
	{
		job.Result = null;
		job.Error = null;
		job.StartedAt = null;
		job.CompletedAt = null;

		var result = JobResponseMapper.ToResponse(job);

		Assert.NotNull(result);
		Assert.Equal(job.Id, result.Id);
		Assert.Equal(job.Name, result.Name);
		Assert.Equal(string.Empty, result.Result);
		Assert.Null(result.Error);
	}
}
