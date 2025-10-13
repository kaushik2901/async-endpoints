using AsyncEndpoints.Handlers;
using AsyncEndpoints.UnitTests.TestSupport;

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
		var query = new List<KeyValuePair<string, List<string?>>> { new("query1", ["value1"]) };

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
		var query = new List<KeyValuePair<string, List<string?>>> { new("query1", ["value1"]) };

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
		var query = new List<KeyValuePair<string, List<string?>>> { new("query1", ["value1"]) };

		// Act
		var context = new AsyncContext<TestRequest>(request, headers, routeParams, query);
		context.RouteParams["newParam"] = "newValue";

		// Assert
		Assert.Equal("newValue", context.RouteParams["newParam"]);
	}

	/// <summary>
	/// Verifies that the base AsyncContext class can be instantiated and its properties are set correctly.
	/// This tests the non-generic base class that is used for no-body requests.
	/// </summary>
	[Fact]
	public void BaseAsyncContext_Constructor_SetsPropertiesCorrectly()
	{
		// Arrange
		var headers = new Dictionary<string, List<string?>> { { "header1", new List<string?> { "value1" } } };
		var routeParams = new Dictionary<string, object?> { { "param1", "value1" } };
		var query = new List<KeyValuePair<string, List<string?>>> { new("query1", ["value1"]) };

		// Act
		var context = new AsyncContext(headers, routeParams, query);

		// Assert
		Assert.Equal(headers, context.Headers);
		Assert.Equal(routeParams, context.RouteParams);
		Assert.Equal(query, context.QueryParams);
	}

	/// <summary>
	/// Verifies that the base AsyncContext inherits correctly from the generic version.
	/// This ensures that both classes have the same base properties available.
	/// </summary>
	[Fact]
	public void BaseAsyncContext_Inheritance_EnablesPolymorphism()
	{
		// Arrange
		var headers = new Dictionary<string, List<string?>> { { "header1", new List<string?> { "value1" } } };
		var routeParams = new Dictionary<string, object?> { { "param1", "value1" } };
		var query = new List<KeyValuePair<string, List<string?>>> { new("query1", ["value1"]) };

		// Act
		var baseContext = new AsyncContext(headers, routeParams, query);
		var genericContext = new AsyncContext<TestRequest>(new TestRequest(), headers, routeParams, query);

		// Assert
		// Both contexts should have the same base properties
		Assert.Equal(baseContext.Headers, genericContext.Headers);
		Assert.Equal(baseContext.RouteParams, genericContext.RouteParams);
		Assert.Equal(baseContext.QueryParams, genericContext.QueryParams);
	}
}
