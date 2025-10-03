using System.Threading.Tasks;
using AsyncEndpoints.Configuration;
using AsyncEndpoints.Infrastructure.Serialization;
using AsyncEndpoints.JobProcessing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncEndpoints.Utilities
{
	/// <summary>
	/// Custom IResult implementation that handles JobResponse serialization 
	/// with proper handling of the Result field to avoid string serialization.
	/// </summary>
	public class JobResultResponse(Job job, int statusCode = 200) : IResult
	{
		private readonly Job _job = job;
		private readonly int _statusCode = statusCode;

		/// <summary>
		/// Creates a JobResultResponse for an accepted job (202 status).
		/// </summary>
		public static JobResultResponse Accepted(Job job)
		{
			return new JobResultResponse(job, StatusCodes.Status202Accepted);
		}

		/// <summary>
		/// Creates a JobResultResponse for an OK response (200 status).
		/// </summary>
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
}
