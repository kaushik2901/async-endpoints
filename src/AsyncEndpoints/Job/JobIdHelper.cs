using System;
using AsyncEndpoints.Constants;
using Microsoft.AspNetCore.Http;

namespace AsyncEndpoints.Job;

internal static class JobIdHelper
{
    public static Guid GetJobId(HttpContext? httpContext)
    {
        if (httpContext == null)
        {
            return Guid.NewGuid();
        }

        if (!httpContext.Request.Headers.TryGetValue(AsyncEndpointConstants.JobIdHeaderName, out var jobIdHeaderValueString))
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