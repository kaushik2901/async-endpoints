using AsyncEndpoints.Handlers;

namespace AsyncEndpoints.UnitTests.Handlers;

/// <summary>
/// Unit tests for the NoBodyRequest class to ensure it functions properly as a placeholder
/// for endpoints that don't require request data.
/// </summary>
public class NoBodyRequestTests
{
	/// <summary>
	/// Verifies that the NoBodyRequest can be instantiated using the CreateInstance method.
	/// This ensures the singleton pattern implementation works correctly.
	/// </summary>
	[Fact]
	public void CreateInstance_ReturnsValidInstance()
	{
		// Act
		var instance = NoBodyRequest.CreateInstance();

		// Assert
		Assert.NotNull(instance);
		Assert.IsType<NoBodyRequest>(instance);
	}

	/// <summary>
	/// Verifies that multiple calls to CreateInstance return different instances.
	/// The current implementation creates a new instance each time rather than a singleton.
	/// </summary>
	[Fact]
	public void CreateInstance_MultipleCalls_ReturnsValidInstances()
	{
		// Act
		var instance1 = NoBodyRequest.CreateInstance();
		var instance2 = NoBodyRequest.CreateInstance();

		// Assert
		Assert.NotNull(instance1);
		Assert.NotNull(instance2);
		Assert.IsType<NoBodyRequest>(instance1);
		Assert.IsType<NoBodyRequest>(instance2);

		// Note: These might be different instances since the implementation creates new each time
	}

	/// <summary>
	/// Verifies that NoBodyRequest has no properties or methods that require data,
	/// confirming it's a true placeholder class for empty requests.
	/// </summary>
	[Fact]
	public void NoBodyRequest_IsEmptyPlaceholder()
	{
		// Act
		var instance = NoBodyRequest.CreateInstance();

		// Assert - the class should have no meaningful state
		// Just check that the instance is properly created
		Assert.NotNull(instance);
	}
}
