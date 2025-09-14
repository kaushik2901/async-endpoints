using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Threading.Tasks;

namespace AsyncEndpoints;

public static class RouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapAsyncPost<TRequest, TResponse>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<HttpContext, Task<IResult?>?>? handler = null)
    {
        return endpoints.MapPost(pattern, (HttpContext httpContext, TRequest request) => 
            RouteBuilderExtensionsHelpers.HandleAsyncEndpoint(httpContext, request, handler));
    }

    public static IEndpointConventionBuilder MapAsyncPut<TRequest, TResponse>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<HttpContext, Task<IResult?>?>? handler = null)
    {
        return endpoints.MapPut(pattern, (HttpContext httpContext, TRequest request) => 
            RouteBuilderExtensionsHelpers.HandleAsyncEndpoint(httpContext, request, handler));
    }

    public static IEndpointConventionBuilder MapAsyncPatch<TRequest, TResponse>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<HttpContext, Task<IResult?>?>? handler = null)
    {
        return endpoints.MapPatch(pattern, (HttpContext httpContext, TRequest request) => 
            RouteBuilderExtensionsHelpers.HandleAsyncEndpoint(httpContext, request, handler));
    }

    public static IEndpointConventionBuilder MapAsyncDelete<TRequest, TResponse>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<HttpContext, Task<IResult?>?>? handler = null)
    {
        return endpoints.MapDelete(pattern, (HttpContext httpContext, TRequest request) => 
            RouteBuilderExtensionsHelpers.HandleAsyncEndpoint(httpContext, request, handler));
    }
}
