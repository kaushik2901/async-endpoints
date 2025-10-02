using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Utilities;

namespace AsyncEndpoints.Handlers;

/// <summary>
/// Defines a contract for handling asynchronous endpoint requests of type TRequest and returning responses of type TResponse.
/// </summary>
/// <typeparam name="TRequest">The type of the request object.</typeparam>
/// <typeparam name="TResponse">The type of the response object.</typeparam>
public interface IAsyncEndpointRequestHandler<TRequest, TResponse>
{
	/// <summary>
	/// Handles the asynchronous request and returns a result.
	/// </summary>
	/// <param name="context">The context containing the request object and associated HTTP context information.</param>
	/// <param name="token">A cancellation token to cancel the operation.</param>
	/// <returns>A <see cref="MethodResult{TResponse}"/> containing the result of the operation.</returns>
	Task<MethodResult<TResponse>> HandleAsync(AsyncContext<TRequest> context, CancellationToken token);
}
