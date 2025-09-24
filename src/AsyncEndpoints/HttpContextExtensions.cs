using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace AsyncEndpoints;

public static class HttpContextExtensions
{
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

    public static Dictionary<string, List<string?>> GetHeadersFromContext(this HttpContext context)
    {
        var headers = new Dictionary<string, List<string?>>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in context.Request.Headers)
        {
            headers[header.Key] = [.. header.Value];
        }
        return headers;
    }

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