using AsyncEndpoints.API.Models;
using AsyncEndpoints.AsyncEndpointRequestHandler;
using AsyncEndpoints.Context;

namespace AsyncEndpoints.API.Services;

public class SampleRequestHandler : IAsyncEndpointRequestHandler<SampleRequest, SampleResponse>
{
    public Task<SampleResponse> HandleAsync(AsyncContext<SampleRequest> context, CancellationToken token)
    {
        throw new NotImplementedException();
    }
}
