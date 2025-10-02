using System;
using System.Threading.Tasks;
using AsyncEndpoints.JobProcessing;
using Microsoft.AspNetCore.Http;

namespace AsyncEndpoints.Utilities;

public static class ResponseDefaults
{
	public static Task<IResult> DefaultJobSubmittedResponseFactory(Job job, HttpContext _)
	{
		var jobResponse = JobResponseMapper.ToResponse(job);
		return Task.FromResult(Results.Accepted("", jobResponse));
	}

	public static Task<IResult> DefaultJobStatusResponseFactory(MethodResult<Job> jobResult, HttpContext _)
	{
		if (!jobResult.IsSuccess)
		{
			return Task.FromResult(Results.Problem(
				detail: jobResult.Error?.Message ?? "An unknown error occurred while submitting the job",
				title: "Job Submission Failed",
				statusCode: 500
			));
		}

		var jobResponse = JobResponseMapper.ToResponse(jobResult.Data);
		return Task.FromResult(Results.Ok(jobResponse));
	}

	public static Task<IResult> DefaultJobSubmissionErrorResponseFactory(AsyncEndpointError? error, HttpContext _)
	{
		return Task.FromResult(Results.Problem(
			detail: error?.Message ?? "An unknown error occurred while submitting the job",
			title: "Job Submission Failed",
			statusCode: 500
		));
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
