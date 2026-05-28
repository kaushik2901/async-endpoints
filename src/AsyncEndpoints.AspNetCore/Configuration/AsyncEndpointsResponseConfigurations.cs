using AsyncEndpoints.Abstractions.Common;
using AsyncEndpoints.AspNetCore.Endpoints;
using AsyncEndpoints.Core.Legacy.JobProcessing;
using Microsoft.AspNetCore.Http;

namespace AsyncEndpoints.AspNetCore.Configuration;

[Obsolete("Use standard ASP.NET patterns (IResult) directly in endpoint code instead.")]
public sealed class AsyncEndpointsResponseConfigurations
{
	public Func<Job, HttpContext, Task<IResult>> JobSubmittedResponseFactory { get; set; }
	public Func<MethodResult<Job>, HttpContext, Task<IResult>> JobStatusResponseFactory { get; set; }
	public Func<AsyncEndpointError?, HttpContext, Task<IResult>> JobSubmissionErrorResponseFactory { get; set; }
	public Func<Exception, HttpContext, Task<IResult>> ExceptionResponseFactory { get; set; }

	public AsyncEndpointsResponseConfigurations()
	{
		JobSubmittedResponseFactory = ResponseDefaults.DefaultJobSubmittedResponseFactory;
		JobStatusResponseFactory = ResponseDefaults.DefaultJobStatusResponseFactory;
		JobSubmissionErrorResponseFactory = ResponseDefaults.DefaultJobSubmissionErrorResponseFactory;
		ExceptionResponseFactory = ResponseDefaults.DefaultExceptionResponseFactory;
	}
}
