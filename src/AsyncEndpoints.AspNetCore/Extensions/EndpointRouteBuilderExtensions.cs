using AsyncEndpoints.AspNetCore.Configuration;
using AsyncEndpoints.AspNetCore.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.AspNetCore.Extensions;

public static class EndpointRouteBuilderExtensions
{
	public static IEndpointRouteBuilder MapAsyncEndpointsEndpoints(
		this IEndpointRouteBuilder routes,
		string? prefix = null)
	{
		var resolvedPrefix = prefix ?? "/jobs";

		routes.MapPost(resolvedPrefix, JobEndpoints.PostJob);
		routes.MapGet($"{resolvedPrefix}/{{jobId:guid}}", JobStatusEndpoint.GetJobStatus);
		routes.MapGet($"{resolvedPrefix}/{{jobId:guid}}/result", JobResultEndpoint.GetJobResult);

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
