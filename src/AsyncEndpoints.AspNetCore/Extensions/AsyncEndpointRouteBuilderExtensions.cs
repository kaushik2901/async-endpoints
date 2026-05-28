using AsyncEndpoints.Abstractions.Submission;
using AsyncEndpoints.AspNetCore.Configuration;
using AsyncEndpoints.AspNetCore.Endpoints;
using AsyncEndpoints.AspNetCore.Models;
using AsyncEndpoints.Core.Infrastructure.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace AsyncEndpoints.AspNetCore.Extensions;

public static class AsyncEndpointRouteBuilderExtensions
{
	public static IEndpointConventionBuilder MapAsyncPost<TRequest>(
		this IEndpointRouteBuilder endpoints,
		string jobName,
		string pattern,
		Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null) =>
		endpoints.MapPost(pattern, CreateRequestDelegate(jobName, handler))
		.WithTags("AsyncEndpoint");

	public static IEndpointConventionBuilder MapAsyncPost(
		this IEndpointRouteBuilder endpoints,
		string jobName,
		string pattern,
		Func<HttpContext, CancellationToken, Task<IResult?>?>? handler = null) =>
		endpoints.MapPost(pattern, CreateRequestDelegate(jobName, handler))
		.WithTags("AsyncEndpoint");

	public static IEndpointConventionBuilder MapAsyncPut<TRequest>(
		this IEndpointRouteBuilder endpoints,
		string jobName,
		string pattern,
		Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null) =>
		endpoints.MapPut(pattern, CreateRequestDelegate(jobName, handler))
		.WithTags("AsyncEndpoint");

	public static IEndpointConventionBuilder MapAsyncPut(
		this IEndpointRouteBuilder endpoints,
		string jobName,
		string pattern,
		Func<HttpContext, CancellationToken, Task<IResult?>?>? handler = null) =>
		endpoints.MapPut(pattern, CreateRequestDelegate(jobName, handler))
		.WithTags("AsyncEndpoint");

	public static IEndpointConventionBuilder MapAsyncPatch<TRequest>(
		this IEndpointRouteBuilder endpoints,
		string jobName,
		string pattern,
		Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null) =>
		endpoints.MapPatch(pattern, CreateRequestDelegate(jobName, handler))
		.WithTags("AsyncEndpoint");

	public static IEndpointConventionBuilder MapAsyncPatch(
		this IEndpointRouteBuilder endpoints,
		string jobName,
		string pattern,
		Func<HttpContext, CancellationToken, Task<IResult?>?>? handler = null) =>
		endpoints.MapPatch(pattern, CreateRequestDelegate(jobName, handler))
		.WithTags("AsyncEndpoint");

	public static IEndpointConventionBuilder MapAsyncDelete<TRequest>(
		this IEndpointRouteBuilder endpoints,
		string jobName,
		string pattern,
		Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null) =>
		endpoints.MapDelete(pattern, CreateRequestDelegate(jobName, handler))
		.WithTags("AsyncEndpoint");

	public static IEndpointConventionBuilder MapAsyncDelete(
		this IEndpointRouteBuilder endpoints,
		string jobName,
		string pattern,
		Func<HttpContext, CancellationToken, Task<IResult?>?>? handler = null) =>
		endpoints.MapDelete(pattern, CreateRequestDelegate(jobName, handler))
		.WithTags("AsyncEndpoint");

	public static IEndpointConventionBuilder MapAsyncGetJobDetails(
		this IEndpointRouteBuilder endpoints,
		string pattern = "/jobs/{jobId:guid}") =>
		endpoints.MapGet(pattern, CreateJobDetailsRequestDelegate())
		.WithTags("AsyncEndpoint");

	private static RequestDelegate CreateRequestDelegate<TRequest>(
		string jobName,
		Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler)
	{
		return async httpContext =>
		{
			var submitter = httpContext.RequestServices.GetRequiredService<IJobSubmitter>();
			var responseConfig = httpContext.RequestServices.GetRequiredService<AsyncEndpointsResponseConfigurations>();
			var serializer = httpContext.RequestServices.GetRequiredService<ISerializer>();
			var ct = httpContext.RequestAborted;

			string bodyText;
			try
			{
				using var reader = new StreamReader(httpContext.Request.Body);
				bodyText = await reader.ReadToEndAsync(ct);
			}
			catch (Exception ex)
			{
				await Results.Problem(detail: ex.Message, statusCode: 400).ExecuteAsync(httpContext);
				return;
			}

			TRequest? request;
			try
			{
				request = DeserializeBody<TRequest>(serializer, bodyText);
			}
			catch (Exception ex)
			{
				await Results.Problem(detail: $"Invalid request body: {ex.Message}", statusCode: 400).ExecuteAsync(httpContext);
				return;
			}

			if (handler is not null)
			{
				var handlerResult = await handler(httpContext, request!, ct);
				if (handlerResult is not null)
				{
					await handlerResult.ExecuteAsync(httpContext);
					return;
				}
			}

			await SubmitJobAndExecute(jobName, httpContext, submitter, responseConfig, bodyText, ct);
		};
	}

	private static RequestDelegate CreateRequestDelegate(
		string jobName,
		Func<HttpContext, CancellationToken, Task<IResult?>?>? handler)
	{
		return async httpContext =>
		{
			var submitter = httpContext.RequestServices.GetRequiredService<IJobSubmitter>();
			var responseConfig = httpContext.RequestServices.GetRequiredService<AsyncEndpointsResponseConfigurations>();
			var ct = httpContext.RequestAborted;

			if (handler is not null)
			{
				var handlerResult = await handler(httpContext, ct);
				if (handlerResult is not null)
				{
					await handlerResult.ExecuteAsync(httpContext);
					return;
				}
			}

			var bodyText = await ReadBodyAsync(httpContext, ct);
			await SubmitJobAndExecute(jobName, httpContext, submitter, responseConfig, bodyText, ct);
		};
	}

	private static RequestDelegate CreateJobDetailsRequestDelegate()
	{
		return async httpContext =>
		{
			await Results.Ok().ExecuteAsync(httpContext);
		};
	}

	private static async Task<string> ReadBodyAsync(HttpContext httpContext, CancellationToken ct)
	{
		using var reader = new StreamReader(httpContext.Request.Body);
		return await reader.ReadToEndAsync(ct);
	}

	private static TRequest? DeserializeBody<TRequest>(ISerializer serializer, string bodyText)
	{
		var typeInfo = (JsonTypeInfo<TRequest>?)AsyncEndpointsAspNetCoreJsonSerializationContext.Default.GetTypeInfo(typeof(TRequest));
		if (typeInfo is not null)
			return serializer.Deserialize(bodyText, typeInfo);

		var coreTypeInfo = (JsonTypeInfo<TRequest>?)AsyncEndpointsJsonSerializationContext.Default.GetTypeInfo(typeof(TRequest));
		if (coreTypeInfo is not null)
			return serializer.Deserialize(bodyText, coreTypeInfo);

		return serializer.Deserialize<TRequest>(bodyText, (JsonSerializerOptions?)null);
	}

	private static async Task SubmitJobAndExecute(
		string jobName,
		HttpContext httpContext,
		IJobSubmitter submitter,
		AsyncEndpointsResponseConfigurations responseConfig,
		string bodyText,
		CancellationToken ct)
	{
		var channel = httpContext.Request.Headers["X-Channel"].FirstOrDefault() ?? "default";
		var partitionKey = httpContext.Request.Headers["X-Partition-Key"].FirstOrDefault();

		var httpPayload = new HttpJobPayload
		{
			Body = bodyText,
			Headers = httpContext.GetHeadersFromContext(),
			RouteParams = httpContext.GetRouteParamsFromContext(),
			QueryParams = httpContext.GetQueryParamsFromContext()
		};

		try
		{
			var jsonPayload = JsonSerializer.Serialize(httpPayload, AsyncEndpointsAspNetCoreJsonSerializationContext.Default.HttpJobPayload);
			var jobId = await submitter.SubmitRawAsync(jobName, jsonPayload, channel, partitionKey, ct);
			var response = await responseConfig.JobSubmittedResponseFactory(jobId, httpContext);
			await response.ExecuteAsync(httpContext);
		}
		catch (Exception ex)
		{
			await Results.Problem(
				detail: ex.Message,
				title: "Job submission failed",
				statusCode: StatusCodes.Status500InternalServerError).ExecuteAsync(httpContext);
		}
	}
}
