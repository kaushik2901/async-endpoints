using AsyncEndpoints.Handlers;

namespace AsyncEndpoints.UnitTests.Utilities;

public class AsyncContextTests
{
	[Fact]
	public void Constructor_SetsPropertiesCorrectly()
	{
		// Arrange
		var request = new TestRequest { Value = "test" };
		var headers = new Dictionary<string, List<string?>> { { "header1", new List<string?> { "value1" } } };
		var routeParams = new Dictionary<string, object?> { { "param1", "value1" } };
		var query = new List<KeyValuePair<string, List<string?>>> { new KeyValuePair<string, List<string?>>("query1", new List<string?> { "value1" }) };

		// Act
		var context = new AsyncContext<TestRequest>(request, headers, routeParams, query);

		// Assert
		Assert.Equal(request, context.Request);
		Assert.Equal(headers, context.Headers);
		Assert.Equal(routeParams, context.RouteParams);
		Assert.Equal(query, context.QueryParams);
	}

	[Fact]
	public void Properties_AreInitializedCorrectly()
	{
		// Arrange
		var request = new TestRequest { Value = "test" };
		var headers = new Dictionary<string, List<string?>> { { "header1", new List<string?> { "value1" } } };
		var routeParams = new Dictionary<string, object?> { { "param1", "value1" } };
		var query = new List<KeyValuePair<string, List<string?>>> { new KeyValuePair<string, List<string?>>("query1", new List<string?> { "value1" }) };

		// Act
		var context = new AsyncContext<TestRequest>(request, headers, routeParams, query);

		// Assert
		Assert.Same(request, context.Request);
		Assert.Same(headers, context.Headers);
		Assert.Same(routeParams, context.RouteParams);
		Assert.Same(query, context.QueryParams);
	}

	[Fact]
	public void RouteParams_AreMutable()
	{
		// Arrange
		var request = new TestRequest { Value = "test" };
		var headers = new Dictionary<string, List<string?>> { { "header1", new List<string?> { "value1" } } };
		var routeParams = new Dictionary<string, object?> { { "param1", "value1" } };
		var query = new List<KeyValuePair<string, List<string?>>> { new KeyValuePair<string, List<string?>>("query1", new List<string?> { "value1" }) };

		// Act
		var context = new AsyncContext<TestRequest>(request, headers, routeParams, query);
		context.RouteParams["newParam"] = "newValue";

		// Assert
		Assert.Equal("newValue", context.RouteParams["newParam"]);
	}
}

public class TestRequest
{
	public string? Value { get; set; }
}
