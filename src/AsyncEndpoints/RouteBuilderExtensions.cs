using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace AsyncEndpoints;

/// <summary>
/// Extension methods for mapping asynchronous endpoints in ASP.NET Core.
/// </summary>
public static class RouteBuilderExtensions
{
	/// <summary>
	/// Maps an asynchronous POST endpoint that processes requests in the background.
	/// The endpoint accepts a request of type TRequest and processes it asynchronously.
	/// </summary>
	/// <typeparam name="TRequest">The type of the request object.</typeparam>
	/// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
	/// <param name="name">A unique name for the async job, used for identifying the handler.</param>
	/// <param name="pattern">The URL pattern for the endpoint.</param>
	/// <param name="handler">Optional custom handler function to process the request. 
	/// If not provided, the default handler will be used based on registered IAsyncEndpointRequestHandler services.</param>
	/// <returns>An <see cref="IEndpointConventionBuilder"/> that can be used to further configure the endpoint.</returns>
	public static IEndpointConventionBuilder MapAsyncPost<TRequest>(
		this IEndpointRouteBuilder endpoints,
		string name,
		string pattern,
		Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null)
	{
		return endpoints
			.MapPost(pattern, Handle(name, handler))
			.WithTags(AsyncEndpointsConstants.AsyncEndpointTag);
	}

	/// <summary>
	/// Maps an asynchronous GET endpoint that fetches job responses by job ID.
	/// </summary>
	/// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
	/// <param name="pattern">The URL pattern for the endpoint. Should contain a {jobId} parameter.</param>
	/// <returns>An <see cref="IEndpointConventionBuilder"/> that can be used to further configure the endpoint.</returns>
	public static IEndpointConventionBuilder MapAsyncGetJobDetails(
		this IEndpointRouteBuilder endpoints,
		string pattern = "/jobs/{jobId:guid}")
	{
		return endpoints
			.MapGet(pattern, GetJobResponse)
			.WithTags(AsyncEndpointsConstants.AsyncEndpointTag);
	}

	private static async Task<IResult> GetJobResponse(
		[FromRoute] Guid jobId,
		[FromServices] IJobResponseService jobResponseService,
		CancellationToken cancellationToken)
	{
		return await jobResponseService.GetJobResponseAsync(jobId, cancellationToken);
	}

	private static Func<HttpContext, TRequest, IAsyncEndpointRequestDelegate, CancellationToken, Task<IResult>> Handle<TRequest>(
		string name,
		Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null)
	{
		return (httpContext, request, [FromServices] asyncEndpointRequestDelegate, token) =>
			asyncEndpointRequestDelegate.HandleAsync(name, httpContext, request, handler, token);
	}
}
