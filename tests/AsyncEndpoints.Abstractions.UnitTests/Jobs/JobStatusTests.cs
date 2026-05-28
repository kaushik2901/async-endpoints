using AsyncEndpoints.Abstractions.Jobs;

namespace AsyncEndpoints.Abstractions.UnitTests.Jobs;

public class JobStatusTests
{
	[Fact]
	public void Queued_Is_100()
	{
		Assert.Equal(100, (int)JobStatus.Queued);
	}

	[Fact]
	public void Processing_Is_300()
	{
		Assert.Equal(300, (int)JobStatus.Processing);
	}

	[Fact]
	public void Completed_Is_500()
	{
		Assert.Equal(500, (int)JobStatus.Completed);
	}

	[Fact]
	public void Failed_Is_600()
	{
		Assert.Equal(600, (int)JobStatus.Failed);
	}

	[Fact]
	public void DeadLettered_Is_700()
	{
		Assert.Equal(700, (int)JobStatus.DeadLettered);
	}
}
