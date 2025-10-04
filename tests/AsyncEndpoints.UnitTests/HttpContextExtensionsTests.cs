using AsyncEndpoints.Configuration;
using AsyncEndpoints.Extensions;
using AsyncEndpoints.UnitTests.TestSupport;
using Microsoft.AspNetCore.Http;

namespace AsyncEndpoints.UnitTests;

public class HttpContextExtensionsTests
{
	[Fact]
	public void GetOrCreateJobId_ReturnsJobIdFromHeader_WhenJobIdHeaderExists()
	{
		// Arrange
		var expectedJobId = Guid.NewGuid();
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Headers[AsyncEndpointsConstants.JobIdHeaderName] = expectedJobId.ToString();

		// Act
		var result = httpContext.GetOrCreateJobId();

		// Assert
		Assert.Equal(expectedJobId, result);
	}

	[Fact]
	public void GetOrCreateJobId_CreatesNewJobId_WhenJobIdHeaderDoesNotExist()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();

		// Act
		var result = httpContext.GetOrCreateJobId();

		// Assert
		Assert.NotEqual(Guid.Empty, result);
	}

	[Theory, AutoMoqData]
	public void GetHeadersFromContext_ReturnsCorrectHeaders(
		string headerName,
		string headerValue)
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Headers[headerName] = headerValue;

		// Act
		var result = httpContext.GetHeadersFromContext();

		// Assert
		Assert.NotNull(result);
		Assert.Contains(headerName, result.Keys);
		Assert.Contains(headerValue, result[headerName]);
	}

	[Theory, AutoMoqData]
	public void GetRouteParamsFromContext_ReturnsCorrectRouteParams(
		string routeParamName,
		string routeParamValue)
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.RouteValues[routeParamName] = routeParamValue;

		// Act
		var result = httpContext.GetRouteParamsFromContext();

		// Assert
		Assert.NotNull(result);
		Assert.Contains(routeParamName, result.Keys);
		Assert.Equal(routeParamValue, result[routeParamName]);
	}

	[Theory, AutoMoqData]
	public void GetQueryParamsFromContext_ReturnsCorrectQueryParams(
		string queryParamName,
		string queryParamValue)
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.Request.QueryString = new QueryString($"?{queryParamName}={queryParamValue}");

		// Act
		var result = httpContext.GetQueryParamsFromContext();

		// Assert
		Assert.NotNull(result);
		var param = result.FirstOrDefault(p => p.Key == queryParamName);
		Assert.NotEqual(default, param);
		Assert.Contains(queryParamValue, param.Value);
	}
}
