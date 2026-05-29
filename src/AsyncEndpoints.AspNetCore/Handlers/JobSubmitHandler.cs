using AsyncEndpoints.Abstractions.Infrastructure.Serialization;
using AsyncEndpoints.Abstractions.Submission;
using AsyncEndpoints.AspNetCore.Configuration;
using AsyncEndpoints.AspNetCore.Extensions;
using AsyncEndpoints.AspNetCore.Infrastructure;
using AsyncEndpoints.AspNetCore.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace AsyncEndpoints.AspNetCore.Handlers;

public static class JobSubmitHandler
{
	public static async Task HandleJobSubmission<TRequest>(
		HttpContext httpContext,
		string jobName,
		Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler,
		IJobSubmitter submitter,
		AsyncEndpointsResponseConfigurations responseConfig,
		ISerializer serializer,
		CancellationToken ct)
	{
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
			var handlerResultTask = handler(httpContext, request!, ct);
			if (handlerResultTask is not null)
			{
				var handlerResult = await handlerResultTask;
				if (handlerResult is not null)
				{
					await handlerResult.ExecuteAsync(httpContext);
					return;
				}
			}
		}

		await SubmitJobAndExecute(jobName, httpContext, submitter, responseConfig, bodyText, ct);
	}

	public static async Task HandleJobSubmission(
		HttpContext httpContext,
		string jobName,
		Func<HttpContext, CancellationToken, Task<IResult?>?>? handler,
		IJobSubmitter submitter,
		AsyncEndpointsResponseConfigurations responseConfig,
		CancellationToken ct)
	{
		if (handler is not null)
		{
			var handlerResultTask = handler(httpContext, ct);
			if (handlerResultTask is not null)
			{
				var handlerResult = await handlerResultTask;
				if (handlerResult is not null)
				{
					await handlerResult.ExecuteAsync(httpContext);
					return;
				}
			}
		}

		var bodyText = await ReadBodyAsync(httpContext, ct);
		await SubmitJobAndExecute(jobName, httpContext, submitter, responseConfig, bodyText, ct);
	}

	internal static RequestDelegate CreateRequestDelegate<TRequest>(
		string jobName,
		Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler)
	{
		return async httpContext =>
		{
			var submitter = httpContext.RequestServices.GetRequiredService<IJobSubmitter>();
			var responseConfig = httpContext.RequestServices.GetRequiredService<AsyncEndpointsResponseConfigurations>();
			var serializer = httpContext.RequestServices.GetRequiredService<ISerializer>();
			await HandleJobSubmission(httpContext, jobName, handler, submitter, responseConfig, serializer, httpContext.RequestAborted);
		};
	}

	internal static RequestDelegate CreateRequestDelegate(
		string jobName,
		Func<HttpContext, CancellationToken, Task<IResult?>?>? handler)
	{
		return async httpContext =>
		{
			var submitter = httpContext.RequestServices.GetRequiredService<IJobSubmitter>();
			var responseConfig = httpContext.RequestServices.GetRequiredService<AsyncEndpointsResponseConfigurations>();
			await HandleJobSubmission(httpContext, jobName, handler, submitter, responseConfig, httpContext.RequestAborted);
		};
	}

	internal static async Task<string> ReadBodyAsync(HttpContext httpContext, CancellationToken ct)
	{
		using var reader = new StreamReader(httpContext.Request.Body);
		return await reader.ReadToEndAsync(ct);
	}

	internal static TRequest? DeserializeBody<TRequest>(ISerializer serializer, string bodyText)
	{
		return serializer.Deserialize<TRequest>(bodyText);
	}

	internal static async Task SubmitJobAndExecute(
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
			var jsonPayload = JsonSerializer.Serialize(httpPayload, AspNetCoreJsonContext.Default.HttpJobPayload);
			var jobId = await submitter.SubmitAsync(jobName, jsonPayload, channel, partitionKey, ct);
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
