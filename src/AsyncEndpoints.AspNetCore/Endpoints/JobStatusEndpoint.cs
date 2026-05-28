using AsyncEndpoints.Abstractions.Storage;
using Microsoft.AspNetCore.Http;

namespace AsyncEndpoints.AspNetCore.Endpoints;

public static class JobStatusEndpoint
{
	public static async Task<IResult> GetJobStatus(
		Guid jobId,
		IJobStore store,
		CancellationToken ct)
	{
		var record = await store.GetStatusAsync(jobId, ct);
		if (record is null)
		{
			return Results.NotFound(new { error = $"Job with id '{jobId}' not found" });
		}

		return Results.Ok(new
		{
			jobId = record.JobId,
			jobName = record.JobName,
			status = record.Status.ToString(),
			result = record.Result,
			errorMessage = record.ErrorMessage,
			createdAt = record.CreatedAt,
			startedAt = record.StartedAt,
			completedAt = record.CompletedAt,
			retryCount = record.RetryCount,
			maxRetries = record.MaxRetries
		});
	}
}
