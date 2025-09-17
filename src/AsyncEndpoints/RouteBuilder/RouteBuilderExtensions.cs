using AsyncEndpoints.Constants;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncEndpoints.RouteBuilder;

public static class RouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapAsyncPost<TRequest>(
        this IEndpointRouteBuilder endpoints,
        string name,
        string pattern,
        Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null)
    {
        return endpoints
            .MapPost(pattern, async (HttpContext httpContext, TRequest request, [FromServices] AsyncEndpointRequestDelegate asyncEndpointRequestDelegate, CancellationToken token) =>
            {
                var value = await RouteBuilderExtensionsHelpers.HandleRequestDelegate(handler, httpContext, request, token);
                if (value != null) return value;
                return await asyncEndpointRequestDelegate.HandleAsync(name, request, token);
            })
            .WithTags(AsyncEndpointConstants.AsyncEndpointTag);
    }
}
