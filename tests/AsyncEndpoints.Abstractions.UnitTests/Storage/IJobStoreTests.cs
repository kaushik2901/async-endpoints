using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Storage;

namespace AsyncEndpoints.Abstractions.UnitTests.Storage;

public class IJobStoreTests
{
	[Fact]
	public void Interface_IsPublic()
	{
		Assert.True(typeof(IJobStore).IsPublic);
	}

	[Fact]
	public void Interface_HasAllMethods()
	{
		var methods = typeof(IJobStore).GetMethods().Select(m => m.Name).ToHashSet();
		Assert.Contains("EnqueueAsync", methods);
		Assert.Contains("DequeueAsync", methods);
		Assert.Contains("UpdateStatusAsync", methods);
		Assert.Contains("GetStatusAsync", methods);
		Assert.Contains("HeartbeatAsync", methods);
		Assert.Contains("ReclaimStaleJobsAsync", methods);
	}

	[Fact]
	public void EnqueueAsync_ReturnsGuid()
	{
		var method = typeof(IJobStore).GetMethod("EnqueueAsync");
		Assert.NotNull(method);
		var returnType = method!.ReturnType;
		Assert.True(returnType == typeof(Task<Guid>) || returnType.GetGenericTypeDefinition() == typeof(Task<>));
	}

	[Fact]
	public void DequeueAsync_ReturnsNullableJobRecord()
	{
		var method = typeof(IJobStore).GetMethod("DequeueAsync");
		Assert.NotNull(method);
		Assert.Equal(typeof(Task<JobRecord?>), method!.ReturnType);
	}
}
