using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Context;

namespace AsyncEndpoints.AsyncEndpointRequestHandler;

public interface IAsyncEndpointRequestHandler<TRequest, TResponse>
{
    Task<TResponse> HandleAsync(AsyncContext<TRequest> context, CancellationToken token);
}
