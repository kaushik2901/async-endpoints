using Microsoft.AspNetCore.Http;

namespace AsyncEndpoints.UnitTests;

public class HttpContextExtensionsTests
{
    [Fact]
    public void GetOrCreateJobId_WhenNoHeader_ReturnsNewGuid()
    {
        // Arrange
        var httpContext = CreateHttpContext();

        // Act
        var result = httpContext.GetOrCreateJobId();

        // Assert
        Assert.NotEqual(Guid.Empty, result);
    }

    [Fact]
    public void GetOrCreateJobId_WhenValidHeader_ReturnsGuidFromHeader()
    {
        // Arrange
        var expectedGuid = Guid.NewGuid();
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers[AsyncEndpointsConstants.JobIdHeaderName] = expectedGuid.ToString();

        // Act
        var result = httpContext.GetOrCreateJobId();

        // Assert
        Assert.Equal(expectedGuid, result);
    }

    [Fact]
    public void GetOrCreateJobId_WhenInvalidHeader_ReturnsNewGuid()
    {
        // Arrange
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers[AsyncEndpointsConstants.JobIdHeaderName] = "invalid-guid";

        // Act
        var result = httpContext.GetOrCreateJobId();

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        Assert.NotEqual("invalid-guid", result.ToString());
    }

    [Fact]
    public void GetOrCreateJobId_WhenEmptyHeader_ReturnsNewGuid()
    {
        // Arrange
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers[AsyncEndpointsConstants.JobIdHeaderName] = "";

        // Act
        var result = httpContext.GetOrCreateJobId();

        // Assert
        Assert.NotEqual(Guid.Empty, result);
    }

    [Fact]
    public void GetHeadersFromContext_ReturnsCorrectHeaders()
    {
        // Arrange
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers["Header1"] = "Value1";
        httpContext.Request.Headers["Header2"] = "Value2";
        httpContext.Request.Headers["header3"] = "Value3"; // Test case-insensitivity

        // Act
        var result = httpContext.GetHeadersFromContext();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("Value1", result["Header1"].First());
        Assert.Equal("Value2", result["Header2"].First());
        Assert.Equal("Value3", result["header3"].First());

        // Test case-insensitive lookup
        Assert.Equal("Value1", result["header1"].First());
        Assert.Equal("Value2", result["HEADER2"].First());
    }

    [Fact]
    public void GetRouteParamsFromContext_ReturnsCorrectRouteParams()
    {
        // Arrange
        var httpContext = CreateHttpContext();
        httpContext.Request.RouteValues["param1"] = "value1";
        httpContext.Request.RouteValues["param2"] = 42;
        httpContext.Request.RouteValues["param3"] = null;

        // Act
        var result = httpContext.GetRouteParamsFromContext();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("value1", result["param1"]);
        Assert.Equal(42, result["param2"]);
        Assert.Null(result["param3"]);
    }

    [Fact]
    public void GetQueryParamsFromContext_ReturnsCorrectQueryParams()
    {
        // Arrange
        var httpContext = CreateHttpContext();
        httpContext.Request.QueryString = new QueryString("?query1=value1&query1=value2&query2=singleValue&query3=");

        // Act
        var result = httpContext.GetQueryParamsFromContext();

        // Assert
        Assert.Equal(3, result.Count);

        var query1 = result.First(q => q.Key == "query1");
        Assert.Equal("query1", query1.Key);
        Assert.Contains("value1", query1.Value);
        Assert.Contains("value2", query1.Value);

        var query2 = result.First(q => q.Key == "query2");
        Assert.Equal("query2", query2.Key);
        Assert.Contains("singleValue", query2.Value);
        Assert.Single(query2.Value);

        var query3 = result.First(q => q.Key == "query3");
        Assert.Equal("query3", query3.Key);
        Assert.Contains("", query3.Value);
    }

    private HttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        return context;
    }
}