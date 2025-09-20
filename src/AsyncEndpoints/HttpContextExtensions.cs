using System;
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
}