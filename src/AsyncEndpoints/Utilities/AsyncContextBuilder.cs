using AsyncEndpoints.Entities;

namespace AsyncEndpoints.Utilities;

/// <summary>
/// Provides methods for building AsyncContext instances from job information.
/// </summary>
public static class AsyncContextBuilder
{
	/// <summary>
	/// Builds an AsyncContext instance from a request object and job information.
	/// </summary>
	/// <typeparam name="TRequest">The type of the request object.</typeparam>
	/// <param name="request">The request object to include in the context.</param>
	/// <param name="job">The job containing headers, route parameters, and query parameters to include in the context.</param>
	/// <returns>A new <see cref="AsyncContext{TRequest}"/> instance containing the request and job information.</returns>
	public static AsyncContext<TRequest> Build<TRequest>(TRequest request, Job job)
	{
		var context = new AsyncContext<TRequest>(
			request,
			job.Headers,
			job.RouteParams,
			job.QueryParams
		);

		return context;
	}
}
