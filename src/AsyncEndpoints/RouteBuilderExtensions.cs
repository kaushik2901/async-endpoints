using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace AsyncEndpoints;

public static class RouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapAsyncPost<TRequest, TResponse>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<HttpContext, Task<IResult?>?>? handler = null)
    {
        return endpoints.MapPost(pattern, (HttpContext httpContext, TRequest request) => HandleAsyncEndpoint(httpContext, request, handler));
    }

    public static IEndpointConventionBuilder MapAsyncPut<TRequest, TResponse>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<HttpContext, Task<IResult?>?>? handler = null)
    {
        return endpoints.MapPut(pattern, (HttpContext httpContext, TRequest request) => HandleAsyncEndpoint(httpContext, request, handler));
    }

    public static IEndpointConventionBuilder MapAsyncPatch<TRequest, TResponse>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<HttpContext, Task<IResult?>?>? handler = null)
    {
        return endpoints.MapPatch(pattern, (HttpContext httpContext, TRequest request) => HandleAsyncEndpoint(httpContext, request, handler));
    }

    public static IEndpointConventionBuilder MapAsyncDelete<TRequest, TResponse>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<HttpContext, Task<IResult?>?>? handler = null)
    {
        return endpoints.MapDelete(pattern, (HttpContext httpContext, TRequest request) => HandleAsyncEndpoint(httpContext, request, handler));
    }

    private static async Task<IResult> HandleAsyncEndpoint<TRequest>(
        HttpContext httpContext,
        TRequest request,
        Func<HttpContext, Task<IResult?>?>? handler = null)
    {
        if (handler != null)
        {
            var handlerResponseTask = handler(httpContext);
            if (handlerResponseTask != null)
            {
                var handlerResponse = await handlerResponseTask;
                if (handlerResponse != null) return handlerResponse;
            }
        }

        var id = Guid.NewGuid(); // TODO: Read from request header/query
        var payLoad = JsonSerializer.Serialize(request);
        var job = Job.Create(id, payLoad);

        // TODO: Complete the implementation.

        return Results.Accepted($"/jobs/{id}");
    }
}
