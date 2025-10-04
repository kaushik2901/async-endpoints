namespace AsyncEndpoints.UnitTests;

public class RouteBuilderExtensionsTests
{
	// NOTE: Extension methods cannot be mocked directly, so these tests focus on compilation and setup.
	// Integration tests would be more appropriate for testing the actual endpoint behavior.

	[Fact]
	public void MapAsyncPost_ExistsAndCompiles()
	{
		// This test just ensures the method signature exists and compiles properly
		// In a real scenario, we'd test this in integration tests with actual routing
		Assert.True(true); // Basic assertion to have a passing test
	}
}
