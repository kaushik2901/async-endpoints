using AsyncEndpoints.AspNetCore.Configuration;
using AsyncEndpoints.AspNetCore.Endpoints;
using AsyncEndpoints.AspNetCore.Models;
using AsyncEndpoints.Abstractions.Submission;
using AsyncEndpoints.Core.Infrastructure.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
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
		endpoints.MapPost(pattern, async (HttpContext httpContext,
			[FromServices] IJobSubmitter submitter,
			[FromServices] AsyncEndpointsResponseConfigurations responseConfig,
			[FromServices] ISerializer serializer,
			CancellationToken ct) =>
		{
			string bodyText;
			try
			{
				using var reader = new StreamReader(httpContext.Request.Body);
				bodyText = await reader.ReadToEndAsync(ct);
			}
			catch (Exception ex)
			{
				return Results.Problem(detail: ex.Message, statusCode: 400);
			}

			TRequest? request;
			try
			{
				request = DeserializeBody<TRequest>(serializer, bodyText);
			}
			catch (Exception ex)
			{
				return Results.Problem(detail: $"Invalid request body: {ex.Message}", statusCode: 400);
			}

			if (handler is not null)
			{
				var handlerResult = await handler(httpContext, request!, ct);
				if (handlerResult is not null)
					return handlerResult;
			}

			return await SubmitJobAndReturn(jobName, httpContext, submitter, responseConfig, bodyText, ct);
		}).WithTags("AsyncEndpoint");

	public static IEndpointConventionBuilder MapAsyncPost(
		this IEndpointRouteBuilder endpoints,
		string jobName,
		string pattern,
		Func<HttpContext, CancellationToken, Task<IResult?>?>? handler = null) =>
		endpoints.MapPost(pattern, async (HttpContext httpContext,
			[FromServices] IJobSubmitter submitter,
			[FromServices] AsyncEndpointsResponseConfigurations responseConfig,
			CancellationToken ct) =>
		{
			if (handler is not null)
			{
				var handlerResult = await handler(httpContext, ct);
				if (handlerResult is not null)
					return handlerResult;
			}

			var bodyText = await ReadBodyAsync(httpContext, ct);
			return await SubmitJobAndReturn(jobName, httpContext, submitter, responseConfig, bodyText, ct);
		}).WithTags("AsyncEndpoint");

	public static IEndpointConventionBuilder MapAsyncPut<TRequest>(
		this IEndpointRouteBuilder endpoints,
		string jobName,
		string pattern,
		Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null) =>
		endpoints.MapPut(pattern, async (HttpContext httpContext,
			[FromServices] IJobSubmitter submitter,
			[FromServices] AsyncEndpointsResponseConfigurations responseConfig,
			[FromServices] ISerializer serializer,
			CancellationToken ct) =>
		{
			string bodyText;
			try
			{
				using var reader = new StreamReader(httpContext.Request.Body);
				bodyText = await reader.ReadToEndAsync(ct);
			}
			catch (Exception ex)
			{
				return Results.Problem(detail: ex.Message, statusCode: 400);
			}

			TRequest? request;
			try
			{
				request = DeserializeBody<TRequest>(serializer, bodyText);
			}
			catch (Exception ex)
			{
				return Results.Problem(detail: $"Invalid request body: {ex.Message}", statusCode: 400);
			}

			if (handler is not null)
			{
				var handlerResult = await handler(httpContext, request!, ct);
				if (handlerResult is not null)
					return handlerResult;
			}

			return await SubmitJobAndReturn(jobName, httpContext, submitter, responseConfig, bodyText, ct);
		}).WithTags("AsyncEndpoint");

	public static IEndpointConventionBuilder MapAsyncPut(
		this IEndpointRouteBuilder endpoints,
		string jobName,
		string pattern,
		Func<HttpContext, CancellationToken, Task<IResult?>?>? handler = null) =>
		endpoints.MapPut(pattern, async (HttpContext httpContext,
			[FromServices] IJobSubmitter submitter,
			[FromServices] AsyncEndpointsResponseConfigurations responseConfig,
			CancellationToken ct) =>
		{
			if (handler is not null)
			{
				var handlerResult = await handler(httpContext, ct);
				if (handlerResult is not null)
					return handlerResult;
			}

			var bodyText = await ReadBodyAsync(httpContext, ct);
			return await SubmitJobAndReturn(jobName, httpContext, submitter, responseConfig, bodyText, ct);
		}).WithTags("AsyncEndpoint");

	public static IEndpointConventionBuilder MapAsyncPatch<TRequest>(
		this IEndpointRouteBuilder endpoints,
		string jobName,
		string pattern,
		Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null) =>
		endpoints.MapPatch(pattern, async (HttpContext httpContext,
			[FromServices] IJobSubmitter submitter,
			[FromServices] AsyncEndpointsResponseConfigurations responseConfig,
			[FromServices] ISerializer serializer,
			CancellationToken ct) =>
		{
			string bodyText;
			try
			{
				using var reader = new StreamReader(httpContext.Request.Body);
				bodyText = await reader.ReadToEndAsync(ct);
			}
			catch (Exception ex)
			{
				return Results.Problem(detail: ex.Message, statusCode: 400);
			}

			TRequest? request;
			try
			{
				request = DeserializeBody<TRequest>(serializer, bodyText);
			}
			catch (Exception ex)
			{
				return Results.Problem(detail: $"Invalid request body: {ex.Message}", statusCode: 400);
			}

			if (handler is not null)
			{
				var handlerResult = await handler(httpContext, request!, ct);
				if (handlerResult is not null)
					return handlerResult;
			}

			return await SubmitJobAndReturn(jobName, httpContext, submitter, responseConfig, bodyText, ct);
		}).WithTags("AsyncEndpoint");

	public static IEndpointConventionBuilder MapAsyncPatch(
		this IEndpointRouteBuilder endpoints,
		string jobName,
		string pattern,
		Func<HttpContext, CancellationToken, Task<IResult?>?>? handler = null) =>
		endpoints.MapPatch(pattern, async (HttpContext httpContext,
			[FromServices] IJobSubmitter submitter,
			[FromServices] AsyncEndpointsResponseConfigurations responseConfig,
			CancellationToken ct) =>
		{
			if (handler is not null)
			{
				var handlerResult = await handler(httpContext, ct);
				if (handlerResult is not null)
					return handlerResult;
			}

			var bodyText = await ReadBodyAsync(httpContext, ct);
			return await SubmitJobAndReturn(jobName, httpContext, submitter, responseConfig, bodyText, ct);
		}).WithTags("AsyncEndpoint");

	public static IEndpointConventionBuilder MapAsyncDelete<TRequest>(
		this IEndpointRouteBuilder endpoints,
		string jobName,
		string pattern,
		Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null) =>
		endpoints.MapDelete(pattern, async (HttpContext httpContext,
			[FromServices] IJobSubmitter submitter,
			[FromServices] AsyncEndpointsResponseConfigurations responseConfig,
			[FromServices] ISerializer serializer,
			CancellationToken ct) =>
		{
			string bodyText;
			try
			{
				using var reader = new StreamReader(httpContext.Request.Body);
				bodyText = await reader.ReadToEndAsync(ct);
			}
			catch (Exception ex)
			{
				return Results.Problem(detail: ex.Message, statusCode: 400);
			}

			TRequest? request;
			try
			{
				request = DeserializeBody<TRequest>(serializer, bodyText);
			}
			catch (Exception ex)
			{
				return Results.Problem(detail: $"Invalid request body: {ex.Message}", statusCode: 400);
			}

			if (handler is not null)
			{
				var handlerResult = await handler(httpContext, request!, ct);
				if (handlerResult is not null)
					return handlerResult;
			}

			return await SubmitJobAndReturn(jobName, httpContext, submitter, responseConfig, bodyText, ct);
		}).WithTags("AsyncEndpoint");

	public static IEndpointConventionBuilder MapAsyncDelete(
		this IEndpointRouteBuilder endpoints,
		string jobName,
		string pattern,
		Func<HttpContext, CancellationToken, Task<IResult?>?>? handler = null) =>
		endpoints.MapDelete(pattern, async (HttpContext httpContext,
			[FromServices] IJobSubmitter submitter,
			[FromServices] AsyncEndpointsResponseConfigurations responseConfig,
			CancellationToken ct) =>
		{
			if (handler is not null)
			{
				var handlerResult = await handler(httpContext, ct);
				if (handlerResult is not null)
					return handlerResult;
			}

			var bodyText = await ReadBodyAsync(httpContext, ct);
			return await SubmitJobAndReturn(jobName, httpContext, submitter, responseConfig, bodyText, ct);
		}).WithTags("AsyncEndpoint");

	public static IEndpointConventionBuilder MapAsyncGetJobDetails(
		this IEndpointRouteBuilder endpoints,
		string pattern = "/jobs/{jobId:guid}") =>
		endpoints.MapGet(pattern, async (HttpContext httpContext,
			[FromRoute] Guid jobId,
			[FromServices] IJobSubmitter submitter) =>
		{
			return Results.Ok();
		}).WithTags("AsyncEndpoint");

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

	private static async Task<IResult> SubmitJobAndReturn(
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

		Guid jobId;
		try
		{
			var jsonPayload = JsonSerializer.Serialize(httpPayload, AsyncEndpointsAspNetCoreJsonSerializationContext.Default.HttpJobPayload);
			jobId = await submitter.SubmitRawAsync(jobName, jsonPayload, channel, partitionKey, ct);
		}
		catch (Exception ex)
		{
			return Results.Problem(
				detail: ex.Message,
				title: "Job submission failed",
				statusCode: StatusCodes.Status500InternalServerError);
		}

		return await responseConfig.JobSubmittedResponseFactory(jobId, httpContext);
	}
}
