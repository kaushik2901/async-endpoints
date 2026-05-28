using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Storage;
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
			return Results.NotFound(new { error = $"Job with id '{jobId}' not found" });
		}

		if (record.Status == JobStatus.Completed)
		{
			return Results.Ok(new
			{
				jobId = record.JobId,
				result = record.Result
			});
		}

		if (record.Status == JobStatus.Failed || record.Status == JobStatus.DeadLettered)
		{
			return Results.Conflict(new
			{
				jobId = record.JobId,
				status = record.Status.ToString(),
				errorMessage = record.ErrorMessage
			});
		}

		return Results.Conflict(new
		{
			jobId = record.JobId,
			status = record.Status.ToString(),
			message = "Job has not yet completed"
		});
	}
}
