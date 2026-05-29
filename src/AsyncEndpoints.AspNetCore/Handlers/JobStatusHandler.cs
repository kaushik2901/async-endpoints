using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.AspNetCore.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncEndpoints.AspNetCore.Handlers;

public static class JobStatusHandler
{
	internal static RequestDelegate CreateRequestDelegate()
	{
		return async httpContext =>
		{
			var store = httpContext.RequestServices.GetRequiredService<IJobStore>();
			var jobId = Guid.Parse((string)httpContext.Request.RouteValues["jobId"]!);
			var result = await GetJobStatus(jobId, store, httpContext.RequestAborted);
			await result.ExecuteAsync(httpContext);
		};
	}

	public static async Task<IResult> GetJobStatus(
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

		return Results.Ok(new JobStatusResponse
		{
			JobId = record.JobId,
			JobName = record.JobName,
			Status = record.Status.ToString(),
			Result = record.Result,
			ErrorMessage = record.ErrorMessage,
			CreatedAt = record.CreatedAt,
			StartedAt = record.StartedAt,
			CompletedAt = record.CompletedAt,
			RetryCount = record.RetryCount,
			MaxRetries = record.MaxRetries
		});
	}
}
