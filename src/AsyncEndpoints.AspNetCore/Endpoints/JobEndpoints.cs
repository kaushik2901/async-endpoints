using AsyncEndpoints.Abstractions.Submission;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace AsyncEndpoints.AspNetCore.Endpoints;

public static class JobEndpoints
{
	public static async Task<IResult> PostJob(
		HttpContext httpContext,
		IJobSubmitter submitter,
		CancellationToken ct)
	{
		JsonElement body;
		try
		{
			using var reader = new StreamReader(httpContext.Request.Body);
			var bodyText = await reader.ReadToEndAsync(ct);
			if (string.IsNullOrWhiteSpace(bodyText))
			{
				return Results.BadRequest(new { error = "Request body is required" });
			}
			body = JsonSerializer.Deserialize<JsonElement>(bodyText);
		}
		catch (JsonException)
		{
			return Results.BadRequest(new { error = "Invalid JSON in request body" });
		}

		var channel = httpContext.Request.Query["channel"].FirstOrDefault()
			?? httpContext.Request.Headers["X-Channel"].FirstOrDefault()
			?? "default";

		var partitionKey = httpContext.Request.Headers["X-Partition-Key"].FirstOrDefault()
			?? httpContext.Request.Query["partitionKey"].FirstOrDefault();

		Guid jobId;
		try
		{
			jobId = await submitter.SubmitAsync(body, channel, partitionKey, ct);
		}
		catch (Exception ex)
		{
			return Results.Problem(
				detail: ex.Message,
				title: "Job submission failed",
				statusCode: StatusCodes.Status500InternalServerError);
		}

		return Results.Accepted($"/jobs/{jobId}", new { jobId });
	}
}
