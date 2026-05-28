using AsyncEndpoints.AspNetCore.Extensions;
using AsyncEndpoints.AspNetCore.UnitTests.TestSupport;
using Microsoft.AspNetCore.Http;

namespace AsyncEndpoints.AspNetCore.UnitTests;

public class HttpContextExtensionsTests
{
	[Fact]
	public void GetOrCreateJobId_ReturnsJobIdFromHeader_WhenJobIdHeaderExists()
	{
		var expectedJobId = Guid.NewGuid();
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Headers["X-Async-Request-Id"] = expectedJobId.ToString();

		var result = httpContext.GetOrCreateJobId();

		Assert.Equal(expectedJobId, result);
	}

	[Fact]
	public void GetOrCreateJobId_CreatesNewJobId_WhenJobIdHeaderDoesNotExist()
	{
		var httpContext = new DefaultHttpContext();

		var result = httpContext.GetOrCreateJobId();

		Assert.NotEqual(Guid.Empty, result);
	}

	[Theory, AutoMoqData]
	public void GetHeadersFromContext_ReturnsCorrectHeaders(
		string headerName,
		string headerValue)
	{
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Headers[headerName] = headerValue;

		var result = httpContext.GetHeadersFromContext();

		Assert.NotNull(result);
		Assert.Contains(headerName, result.Keys);
		Assert.Contains(headerValue, result[headerName]);
	}

	[Theory, AutoMoqData]
	public void GetRouteParamsFromContext_ReturnsCorrectRouteParams(
		string routeParamName,
		string routeParamValue)
	{
		var httpContext = new DefaultHttpContext();
		httpContext.Request.RouteValues[routeParamName] = routeParamValue;

		var result = httpContext.GetRouteParamsFromContext();

		Assert.NotNull(result);
		Assert.Contains(routeParamName, result.Keys);
		Assert.Equal(routeParamValue, result[routeParamName]);
	}

	[Theory, AutoMoqData]
	public void GetQueryParamsFromContext_ReturnsCorrectQueryParams(
		string queryParamName,
		string queryParamValue)
	{
		var httpContext = new DefaultHttpContext();
		httpContext.Request.QueryString = new QueryString($"?{queryParamName}={queryParamValue}");

		var result = httpContext.GetQueryParamsFromContext();

		Assert.NotNull(result);
		Assert.Contains(queryParamName, result.Keys);
		Assert.Contains(queryParamValue, result[queryParamName]);
	}
}
