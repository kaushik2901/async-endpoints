using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Listener;

namespace AsyncEndpoints.Abstractions.UnitTests.Listener;

public class IJobListenerTests
{
	[Fact]
	public void Interface_IsPublic()
	{
		Assert.True(typeof(IJobListener).IsPublic);
	}

	[Fact]
	public void WaitForNextJobAsync_ReturnsNullableJobRecord()
	{
		var method = typeof(IJobListener).GetMethod("WaitForNextJobAsync");
		Assert.NotNull(method);
		Assert.Equal(typeof(Task<JobRecord?>), method!.ReturnType);
	}
}
