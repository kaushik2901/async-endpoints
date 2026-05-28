using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.AspNetCore.Models;
using Microsoft.AspNetCore.Http;

namespace AsyncEndpoints.AspNetCore.Endpoints;

public static class ResponseDefaults
{
	public static Task<IResult> DefaultJobSubmittedResponseFactory(Guid jobId, HttpContext _)
		=> Task.FromResult(Results.Accepted($"/jobs/{jobId}", new Models.JobSubmittedResponse { JobId = jobId }));

	public static Task<IResult> DefaultJobStatusResponseFactory(JobRecord? record, HttpContext _)
	{
		if (record is null)
			return Task.FromResult(Results.Problem(
				detail: "Job not found", statusCode: 404));

		return Task.FromResult(Results.Ok(new Models.JobStatusResponse
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
		}));
	}

	public static Task<IResult> DefaultJobResultResponseFactory(JobRecord record, HttpContext _)
	{
		if (record.Status == Abstractions.Jobs.JobStatus.Completed)
		{
			return Task.FromResult(Results.Ok(new Models.JobResultResponse
			{
				JobId = record.JobId,
				Result = record.Result
			}));
		}

		return Task.FromResult(Results.Problem(
			detail: record.Status == Abstractions.Jobs.JobStatus.Failed || record.Status == Abstractions.Jobs.JobStatus.DeadLettered
				? record.ErrorMessage ?? "Job failed"
				: "Job has not yet completed",
			statusCode: 409));
	}

	public static Task<IResult> DefaultExceptionResponseFactory(Exception exception, HttpContext _)
	{
		return Task.FromResult(Results.Problem(
			detail: exception.Message,
			title: "An error occurred",
			statusCode: 500
		));
	}
}
