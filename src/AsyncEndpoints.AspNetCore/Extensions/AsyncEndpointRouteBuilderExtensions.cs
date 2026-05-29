using AsyncEndpoints.AspNetCore.Configuration;
using AsyncEndpoints.AspNetCore.Handlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.AspNetCore.Extensions;

public static class AsyncEndpointRouteBuilderExtensions
{
	public static IEndpointConventionBuilder MapAsyncPost<TRequest>(
		this IEndpointRouteBuilder endpoints,
		string jobName,
		string pattern,
		Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null) =>
		endpoints.MapPost(pattern, AsyncEndpointHandler.CreateRequestDelegate(jobName, handler))
		.WithTags("AsyncEndpoint");

	public static IEndpointConventionBuilder MapAsyncPost(
		this IEndpointRouteBuilder endpoints,
		string jobName,
		string pattern,
		Func<HttpContext, CancellationToken, Task<IResult?>?>? handler = null) =>
		endpoints.MapPost(pattern, AsyncEndpointHandler.CreateRequestDelegate(jobName, handler))
		.WithTags("AsyncEndpoint");

	public static IEndpointConventionBuilder MapAsyncPut<TRequest>(
		this IEndpointRouteBuilder endpoints,
		string jobName,
		string pattern,
		Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null) =>
		endpoints.MapPut(pattern, AsyncEndpointHandler.CreateRequestDelegate(jobName, handler))
		.WithTags("AsyncEndpoint");

	public static IEndpointConventionBuilder MapAsyncPut(
		this IEndpointRouteBuilder endpoints,
		string jobName,
		string pattern,
		Func<HttpContext, CancellationToken, Task<IResult?>?>? handler = null) =>
		endpoints.MapPut(pattern, AsyncEndpointHandler.CreateRequestDelegate(jobName, handler))
		.WithTags("AsyncEndpoint");

	public static IEndpointConventionBuilder MapAsyncPatch<TRequest>(
		this IEndpointRouteBuilder endpoints,
		string jobName,
		string pattern,
		Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null) =>
		endpoints.MapPatch(pattern, AsyncEndpointHandler.CreateRequestDelegate(jobName, handler))
		.WithTags("AsyncEndpoint");

	public static IEndpointConventionBuilder MapAsyncPatch(
		this IEndpointRouteBuilder endpoints,
		string jobName,
		string pattern,
		Func<HttpContext, CancellationToken, Task<IResult?>?>? handler = null) =>
		endpoints.MapPatch(pattern, AsyncEndpointHandler.CreateRequestDelegate(jobName, handler))
		.WithTags("AsyncEndpoint");

	public static IEndpointConventionBuilder MapAsyncDelete<TRequest>(
		this IEndpointRouteBuilder endpoints,
		string jobName,
		string pattern,
		Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null) =>
		endpoints.MapDelete(pattern, AsyncEndpointHandler.CreateRequestDelegate(jobName, handler))
		.WithTags("AsyncEndpoint");

	public static IEndpointConventionBuilder MapAsyncDelete(
		this IEndpointRouteBuilder endpoints,
		string jobName,
		string pattern,
		Func<HttpContext, CancellationToken, Task<IResult?>?>? handler = null) =>
		endpoints.MapDelete(pattern, AsyncEndpointHandler.CreateRequestDelegate(jobName, handler))
		.WithTags("AsyncEndpoint");

	public static IEndpointRouteBuilder MapAsyncEndpointsEndpoints(
		this IEndpointRouteBuilder routes,
		string? prefix = null)
	{
		var resolvedPrefix = prefix ?? "/jobs";

		routes.MapGet($"{resolvedPrefix}/{{jobId:guid}}", JobStatusHandler.CreateRequestDelegate());
		routes.MapGet($"{resolvedPrefix}/{{jobId:guid}}/result", JobResultHandler.CreateRequestDelegate());

		return routes;
	}

	public static IEndpointRouteBuilder MapAsyncEndpointsEndpoints(
		this IEndpointRouteBuilder routes,
		AspNetCoreOptions options)
	{
		return routes.MapAsyncEndpointsEndpoints(options.EndpointPrefix);
	}

	public static IEndpointRouteBuilder MapAsyncEndpointsEndpoints(
		this IEndpointRouteBuilder routes,
		IOptions<AspNetCoreOptions> options)
	{
		return routes.MapAsyncEndpointsEndpoints(options.Value.EndpointPrefix);
	}
}
