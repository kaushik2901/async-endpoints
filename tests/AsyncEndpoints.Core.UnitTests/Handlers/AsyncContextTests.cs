using AsyncEndpoints.Core.Legacy.Handlers;
using AsyncEndpoints.Core.UnitTests.TestSupport;

namespace AsyncEndpoints.Core.UnitTests.Handlers;

public class AsyncContextTests
{
	[Fact]
	public void Constructor_SetsPropertiesCorrectly()
	{
		var request = new TestRequest { Value = "test" };
		var headers = new Dictionary<string, List<string?>> { { "header1", new List<string?> { "value1" } } };
		var routeParams = new Dictionary<string, object?> { { "param1", "value1" } };
		var query = new List<KeyValuePair<string, List<string?>>> { new("query1", ["value1"]) };

		var context = new AsyncContext<TestRequest>(request, headers, routeParams, query);

		Assert.Equal(request, context.Request);
		Assert.Equal(headers, context.Headers);
		Assert.Equal(routeParams, context.RouteParams);
		Assert.Equal(query, context.QueryParams);
	}

	[Fact]
	public void Properties_AreInitializedCorrectly()
	{
		var request = new TestRequest { Value = "test" };
		var headers = new Dictionary<string, List<string?>> { { "header1", new List<string?> { "value1" } } };
		var routeParams = new Dictionary<string, object?> { { "param1", "value1" } };
		var query = new List<KeyValuePair<string, List<string?>>> { new("query1", ["value1"]) };

		var context = new AsyncContext<TestRequest>(request, headers, routeParams, query);

		Assert.Same(request, context.Request);
		Assert.Same(headers, context.Headers);
		Assert.Same(routeParams, context.RouteParams);
		Assert.Same(query, context.QueryParams);
	}

	[Fact]
	public void RouteParams_AreMutable()
	{
		var request = new TestRequest { Value = "test" };
		var headers = new Dictionary<string, List<string?>> { { "header1", new List<string?> { "value1" } } };
		var routeParams = new Dictionary<string, object?> { { "param1", "value1" } };
		var query = new List<KeyValuePair<string, List<string?>>> { new("query1", ["value1"]) };

		var context = new AsyncContext<TestRequest>(request, headers, routeParams, query);
		context.RouteParams["newParam"] = "newValue";

		Assert.Equal("newValue", context.RouteParams["newParam"]);
	}

	[Fact]
	public void BaseAsyncContext_Constructor_SetsPropertiesCorrectly()
	{
		var headers = new Dictionary<string, List<string?>> { { "header1", new List<string?> { "value1" } } };
		var routeParams = new Dictionary<string, object?> { { "param1", "value1" } };
		var query = new List<KeyValuePair<string, List<string?>>> { new("query1", ["value1"]) };

		var context = new AsyncContext(headers, routeParams, query);

		Assert.Equal(headers, context.Headers);
		Assert.Equal(routeParams, context.RouteParams);
		Assert.Equal(query, context.QueryParams);
	}

	[Fact]
	public void BaseAsyncContext_Inheritance_EnablesPolymorphism()
	{
		var headers = new Dictionary<string, List<string?>> { { "header1", new List<string?> { "value1" } } };
		var routeParams = new Dictionary<string, object?> { { "param1", "value1" } };
		var query = new List<KeyValuePair<string, List<string?>>> { new("query1", ["value1"]) };

		var baseContext = new AsyncContext(headers, routeParams, query);
		var genericContext = new AsyncContext<TestRequest>(new TestRequest(), headers, routeParams, query);

		Assert.Equal(baseContext.Headers, genericContext.Headers);
		Assert.Equal(baseContext.RouteParams, genericContext.RouteParams);
		Assert.Equal(baseContext.QueryParams, genericContext.QueryParams);
	}
}
