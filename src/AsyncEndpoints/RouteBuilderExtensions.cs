using AsyncEndpoints.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncEndpoints;

public static class RouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapAsyncPost<TRequest>(
        this IEndpointRouteBuilder endpoints,
        string name,
        string pattern,
        Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null)
    {
        return endpoints
            .MapPost(pattern, Handle(name, handler))
            .WithTags(AsyncEndpointConstants.AsyncEndpointTag);
    }

    private static Func<HttpContext, TRequest, IAsyncEndpointRequestDelegate, CancellationToken, Task<IResult>> Handle<TRequest>(
        string name,
        Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null)
    {
        return (httpContext, request, [FromServices] asyncEndpointRequestDelegate, token) =>
            asyncEndpointRequestDelegate.HandleAsync(name, httpContext, request, handler, token);
    }
}
