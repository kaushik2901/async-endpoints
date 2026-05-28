using Microsoft.AspNetCore.Http;

namespace AsyncEndpoints.AspNetCore.Extensions;

public static class HttpContextExtensions
{
	public static Guid GetOrCreateJobId(this HttpContext httpContext)
	{
		if (!httpContext.Request.Headers.TryGetValue("X-Async-Request-Id", out var jobIdHeaderValueString))
		{
			return Guid.NewGuid();
		}

		if (!Guid.TryParse(jobIdHeaderValueString, out var jobIdGuid))
		{
			return Guid.NewGuid();
		}

		return jobIdGuid;
	}

	public static Dictionary<string, List<string?>> GetHeadersFromContext(this HttpContext context)
	{
		var headers = new Dictionary<string, List<string?>>(StringComparer.OrdinalIgnoreCase);
		foreach (var header in context.Request.Headers)
		{
			headers[header.Key] = [.. header.Value];
		}
		return headers;
	}

	public static Dictionary<string, string?> GetRouteParamsFromContext(this HttpContext context)
	{
		var routeParams = new Dictionary<string, string?>();
		var routeValues = context.Request.RouteValues;
		if (routeValues is not null)
		{
			foreach (var routeValue in routeValues)
			{
				routeParams[routeValue.Key] = routeValue.Value?.ToString();
			}
		}
		return routeParams;
	}

	public static Dictionary<string, List<string?>> GetQueryParamsFromContext(this HttpContext context)
	{
		var queryParams = new Dictionary<string, List<string?>>();
		foreach (var queryParam in context.Request.Query)
		{
			queryParams[queryParam.Key] = [.. queryParam.Value];
		}
		return queryParams;
	}
}
