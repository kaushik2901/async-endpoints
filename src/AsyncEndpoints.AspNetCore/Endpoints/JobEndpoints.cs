using AsyncEndpoints.Abstractions.Submission;
using AsyncEndpoints.AspNetCore.Configuration;
using AsyncEndpoints.AspNetCore.Extensions;
using AsyncEndpoints.AspNetCore.Models;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace AsyncEndpoints.AspNetCore.Endpoints;

public static class JobEndpoints
{
	public static async Task<IResult> PostJob(
		HttpContext httpContext,
		IJobSubmitter submitter,
		AsyncEndpointsResponseConfigurations responseConfig,
		CancellationToken ct)
	{
		string bodyText;
		try
		{
			using var reader = new StreamReader(httpContext.Request.Body);
			bodyText = await reader.ReadToEndAsync(ct);
			if (string.IsNullOrWhiteSpace(bodyText))
			{
				return Results.Problem(
					detail: "Request body is required",
					statusCode: 400);
			}
		}
		catch (Exception ex)
		{
			return Results.Problem(
				detail: ex.Message,
				title: "Invalid request body",
				statusCode: 400);
		}

		var channel = httpContext.Request.Query["channel"].FirstOrDefault()
			?? httpContext.Request.Headers["X-Channel"].FirstOrDefault()
			?? "default";

		var partitionKey = httpContext.Request.Headers["X-Partition-Key"].FirstOrDefault()
			?? httpContext.Request.Query["partitionKey"].FirstOrDefault();

		var jobName = httpContext.Request.Headers["X-Job-Name"].FirstOrDefault()
			?? "default";

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
