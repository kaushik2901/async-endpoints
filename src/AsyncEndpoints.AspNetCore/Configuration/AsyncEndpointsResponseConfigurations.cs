using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.AspNetCore.Endpoints;
using Microsoft.AspNetCore.Http;

namespace AsyncEndpoints.AspNetCore.Configuration;

public sealed class AsyncEndpointsResponseConfigurations
{
	public Func<Guid, HttpContext, Task<IResult>> JobSubmittedResponseFactory { get; set; }
	public Func<JobRecord?, HttpContext, Task<IResult>> JobStatusResponseFactory { get; set; }
	public Func<JobRecord, HttpContext, Task<IResult>> JobResultResponseFactory { get; set; }
	public Func<Exception, HttpContext, Task<IResult>> ExceptionResponseFactory { get; set; }

	public AsyncEndpointsResponseConfigurations()
	{
		JobSubmittedResponseFactory = ResponseDefaults.DefaultJobSubmittedResponseFactory;
		JobStatusResponseFactory = ResponseDefaults.DefaultJobStatusResponseFactory;
		JobResultResponseFactory = ResponseDefaults.DefaultJobResultResponseFactory;
		ExceptionResponseFactory = ResponseDefaults.DefaultExceptionResponseFactory;
	}
}
