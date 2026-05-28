using AsyncEndpoints.Abstractions.Partitioning;

namespace AsyncEndpoints.Abstractions.UnitTests.Partitioning;

public class IPartitionAssignerTests
{
	[Fact]
	public void Interface_IsPublic()
	{
		Assert.True(typeof(IPartitionAssigner).IsPublic);
	}

	[Fact]
	public void Interface_HasAllMethods()
	{
		var methods = typeof(IPartitionAssigner).GetMethods().Select(m => m.Name).ToHashSet();
		Assert.Contains("AcquirePartitionAsync", methods);
		Assert.Contains("RenewLeaseAsync", methods);
		Assert.Contains("ReleasePartitionAsync", methods);
	}

	[Fact]
	public void AcquirePartitionAsync_ReturnsInt()
	{
		var method = typeof(IPartitionAssigner).GetMethod("AcquirePartitionAsync");
		Assert.NotNull(method);
		Assert.Equal(typeof(Task<int>), method!.ReturnType);
	}
}
