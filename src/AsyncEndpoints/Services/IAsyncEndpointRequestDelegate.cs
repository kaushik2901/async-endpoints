using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace AsyncEndpoints.Services;

/// <summary>
/// Defines a contract for handling asynchronous endpoint requests and managing their lifecycle.
/// </summary>
public interface IAsyncEndpointRequestDelegate
{
	/// <summary>
	/// Handles an asynchronous request by creating a job and returning an immediate response.
	/// </summary>
	/// <typeparam name="TRequest">The type of the request object.</typeparam>
	/// <param name="jobName">The unique name of the job, used to identify the specific handler.</param>
	/// <param name="httpContext">The HTTP context containing the request information.</param>
	/// <param name="request">The request object to process asynchronously.</param>
	/// <param name="handler">Optional custom handler function to process the request.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>An <see cref="IResult"/> representing the HTTP response.</returns>
	Task<IResult> HandleAsync<TRequest>(string jobName, HttpContext httpContext, TRequest request, Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null, CancellationToken cancellationToken = default);
}
