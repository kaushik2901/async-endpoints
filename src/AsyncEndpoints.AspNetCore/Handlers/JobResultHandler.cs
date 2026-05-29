using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncEndpoints.AspNetCore.Handlers;

public static class JobResultHandler
{
	internal static RequestDelegate CreateRequestDelegate()
	{
		return async httpContext =>
		{
			var store = httpContext.RequestServices.GetRequiredService<IJobStore>();
			var jobId = Guid.Parse((string)httpContext.Request.RouteValues["jobId"]!);
			var result = await GetJobResult(jobId, store, httpContext.RequestAborted);
			await result.ExecuteAsync(httpContext);
		};
	}

	public static async Task<IResult> GetJobResult(
		Guid jobId,
		IJobStore store,
		CancellationToken ct)
	{
		var record = await store.GetStatusAsync(jobId, ct);
		if (record is null)
		{
			return Results.Problem(
				detail: $"Job with id '{jobId}' not found",
				statusCode: 404);
		}

		if (record.Status == JobStatus.Completed)
		{
			return Results.Ok(new Models.JobResultResponse
			{
				JobId = record.JobId,
				Result = record.Result
			});
		}

		if (record.Status == JobStatus.Failed || record.Status == JobStatus.DeadLettered)
		{
			return Results.Problem(
				detail: record.ErrorMessage ?? "Job failed",
				statusCode: 409);
		}

		return Results.Problem(
			detail: "Job has not yet completed",
			statusCode: 409);
	}
}
