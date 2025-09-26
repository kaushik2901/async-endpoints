using AsyncEndpoints;
using AsyncEndpoints.Contracts;
using AsyncEndpoints.Utilities;
using InMemoryExampleAPI.Models;

namespace InMemoryExampleAPI.Services;

public class SampleRequestHandler : IAsyncEndpointRequestHandler<SampleRequest, SampleResponse>
{
    public Task<MethodResult<SampleResponse>> HandleAsync(AsyncContext<SampleRequest> context, CancellationToken token)
    {
        throw new NotImplementedException();
    }
}
