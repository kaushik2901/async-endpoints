using AsyncEndpoints.Core.Legacy.Handlers;

namespace AsyncEndpoints.Core.UnitTests.Handlers;

public class NoBodyRequestTests
{
	[Fact]
	public void CreateInstance_ReturnsValidInstance()
	{
		var instance = NoBodyRequest.CreateInstance();

		Assert.NotNull(instance);
		Assert.IsType<NoBodyRequest>(instance);
	}

	[Fact]
	public void CreateInstance_MultipleCalls_ReturnsValidInstances()
	{
		var instance1 = NoBodyRequest.CreateInstance();
		var instance2 = NoBodyRequest.CreateInstance();

		Assert.NotNull(instance1);
		Assert.NotNull(instance2);
		Assert.IsType<NoBodyRequest>(instance1);
		Assert.IsType<NoBodyRequest>(instance2);
	}

	[Fact]
	public void NoBodyRequest_IsEmptyPlaceholder()
	{
		var instance = NoBodyRequest.CreateInstance();

		Assert.NotNull(instance);
	}
}
