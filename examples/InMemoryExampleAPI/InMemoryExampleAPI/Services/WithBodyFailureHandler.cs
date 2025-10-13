using AsyncEndpoints.Handlers;
using AsyncEndpoints.Utilities;
using InMemoryExampleAPI.Models;

namespace InMemoryExampleAPI.Services;

public class WithBodyFailureHandler : IAsyncEndpointRequestHandler<ExampleRequest, ExampleResponse>
{
    public async Task<MethodResult<ExampleResponse>> HandleAsync(AsyncContext<ExampleRequest> context, CancellationToken token)
    {
        var request = context.Request;

        // Simulate some processing time
        if (request.ProcessingDelaySeconds > 0)
        {
            await Task.Delay(request.ProcessingDelaySeconds * 1000, token);
        }

        // Always fail as this is a failing handler example
        return MethodResult<ExampleResponse>.Failure("This is a simulated failure for demonstration purposes.");
    }
}
