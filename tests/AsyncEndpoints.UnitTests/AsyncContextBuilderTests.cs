using AsyncEndpoints.Entities;
using AsyncEndpoints.Utilities;

namespace AsyncEndpoints.UnitTests;

public class AsyncContextBuilderTests
{
    [Fact]
    public void Build_CreatesAsyncContextWithCorrectProperties()
    {
        // Arrange
        var request = new TestRequest { Value = "test" };
        var headers = new Dictionary<string, List<string?>> { { "header1", new List<string?> { "value1" } } };
        var routeParams = new Dictionary<string, object?> { { "param1", "value1" } };
        var queryParams = new List<KeyValuePair<string, List<string?>>> { new KeyValuePair<string, List<string?>>("query1", new List<string?> { "value1" }) };

        var job = new Job
        {
            Headers = headers,
            RouteParams = routeParams,
            QueryParams = queryParams
        };

        // Act
        var context = AsyncContextBuilder.Build(request, job);

        // Assert
        Assert.Equal(request, context.Request);
        Assert.Equal(headers, context.Headers);
        Assert.Equal(routeParams, context.RouteParams);
        Assert.Equal(queryParams, context.QueryParams);
    }

    [Fact]
    public void Build_WithNullValues_HandlesCorrectly()
    {
        // Arrange
        var request = new TestRequest { Value = "test" };
        var job = new Job
        {
            Headers = new Dictionary<string, List<string?>>(),
            RouteParams = new Dictionary<string, object?>(),
            QueryParams = new List<KeyValuePair<string, List<string?>>>()
        };

        // Act
        var context = AsyncContextBuilder.Build(request, job);

        // Assert
        Assert.Equal(request, context.Request);
        Assert.Equal(job.Headers, context.Headers);
        Assert.Equal(job.RouteParams, context.RouteParams);
        Assert.Equal(job.QueryParams, context.QueryParams);
    }
}