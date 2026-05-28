using AsyncEndpoints.AspNetCore.Models;
using AsyncEndpoints.Core.Configuration;
using AsyncEndpoints.Core.Infrastructure.Serialization;
using AsyncEndpoints.Core.JobProcessing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncEndpoints.AspNetCore.Endpoints;

public class JobResultResponse(Job job, int statusCode = 200) : IResult
{
	private readonly Job _job = job;
	private readonly int _statusCode = statusCode;

	public static JobResultResponse Accepted(Job job)
	{
		return new JobResultResponse(job, StatusCodes.Status202Accepted);
	}

	public static JobResultResponse Ok(Job job)
	{
		return new JobResultResponse(job, StatusCodes.Status200OK);
	}

	public async Task ExecuteAsync(HttpContext httpContext)
	{
		var jobResponse = JobResponseMapper.ToResponse(_job);
		jobResponse.Result = AsyncEndpointsConstants.JobResultPlaceholder;

		var serializer = httpContext.RequestServices.GetRequiredService<ISerializer>();
		var serializedResponse = serializer.Serialize(jobResponse);
		var jobResult = string.IsNullOrEmpty(_job.Result) ? "null" : _job.Result;
		var responseString = serializedResponse.Replace($"\"{AsyncEndpointsConstants.JobResultPlaceholder}\"", jobResult);

		httpContext.Response.StatusCode = _statusCode;
		httpContext.Response.ContentType = "application/json";

		await httpContext.Response.WriteAsync(responseString);
	}
}
