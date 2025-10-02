using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace AsyncEndpoints;

/// <summary>
/// Extension methods for working with HTTP context in AsyncEndpoints.
/// </summary>
public static class HttpContextExtensions
{
	/// <summary>
	/// Gets the job ID from the request headers or creates a new one if not present or invalid.
	/// The job ID is read from the header specified by AsyncEndpointsConstants.JobIdHeaderName.
	/// </summary>
	/// <param name="httpContext">The HTTP context containing the request information.</param>
	/// <returns>The job ID from the headers if valid, otherwise a newly generated GUID.</returns>
	public static Guid GetOrCreateJobId(this HttpContext httpContext)
	{
		if (!httpContext.Request.Headers.TryGetValue(AsyncEndpointsConstants.JobIdHeaderName, out var jobIdHeaderValueString))
		{
			return Guid.NewGuid();
		}

		if (!Guid.TryParse(jobIdHeaderValueString, out var jobIdGuid))
		{
			return Guid.NewGuid();
		}

		return jobIdGuid;
	}

	/// <summary>
	/// Extracts all headers from the HTTP request context into a dictionary.
	/// The keys are case-insensitive.
	/// </summary>
	/// <param name="context">The HTTP context containing the request.</param>
	/// <returns>A dictionary containing all request headers, with header names as keys and header values as lists.</returns>
	public static Dictionary<string, List<string?>> GetHeadersFromContext(this HttpContext context)
	{
		var headers = new Dictionary<string, List<string?>>(StringComparer.OrdinalIgnoreCase);
		foreach (var header in context.Request.Headers)
		{
			headers[header.Key] = [.. header.Value];
		}
		return headers;
	}

	/// <summary>
	/// Extracts route parameters from the HTTP request context into a dictionary.
	/// </summary>
	/// <param name="context">The HTTP context containing the request.</param>
	/// <returns>A dictionary containing route parameter names as keys and their values.</returns>
	public static Dictionary<string, object?> GetRouteParamsFromContext(this HttpContext context)
	{
		var routeParams = new Dictionary<string, object?>();
		var routeValues = context.Request.RouteValues;
		foreach (var routeValue in routeValues)
		{
			routeParams[routeValue.Key] = routeValue.Value;
		}
		return routeParams;
	}

	/// <summary>
	/// Extracts query parameters from the HTTP request context into a list of key-value pairs.
	/// </summary>
	/// <param name="context">The HTTP context containing the request.</param>
	/// <returns>A list of key-value pairs where the key is the query parameter name and the value is a list of parameter values.</returns>
	public static List<KeyValuePair<string, List<string?>>> GetQueryParamsFromContext(this HttpContext context)
	{
		var queryParams = new List<KeyValuePair<string, List<string?>>>();
		foreach (var queryParam in context.Request.Query)
		{
			queryParams.Add(new KeyValuePair<string, List<string?>>(queryParam.Key, [.. queryParam.Value]));
		}
		return queryParams;
	}
}