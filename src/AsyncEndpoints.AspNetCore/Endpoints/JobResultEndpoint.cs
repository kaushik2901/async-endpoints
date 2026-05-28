using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.AspNetCore.Models;
using Microsoft.AspNetCore.Http;

namespace AsyncEndpoints.AspNetCore.Endpoints;

public static class JobResultEndpoint
{
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
