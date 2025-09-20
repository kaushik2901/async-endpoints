using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Utilities;

namespace AsyncEndpoints.Contracts;

public interface IAsyncEndpointRequestHandler<TRequest, TResponse>
{
    Task<MethodResult<TResponse>> HandleAsync(AsyncContext<TRequest> context, CancellationToken token);
}
