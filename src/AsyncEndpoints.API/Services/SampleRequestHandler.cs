using AsyncEndpoints.API.Models;
using AsyncEndpoints.Contracts;
using AsyncEndpoints.Utilities;

namespace AsyncEndpoints.API.Services;

public class SampleRequestHandler : IAsyncEndpointRequestHandler<SampleRequest, SampleResponse>
{
    public Task<MethodResult<SampleResponse>> HandleAsync(AsyncContext<SampleRequest> context, CancellationToken token)
    {
        throw new NotImplementedException();
    }
}
