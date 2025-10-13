using AsyncEndpoints.Handlers;
using AsyncEndpoints.Utilities;
using RedisExampleCore;

namespace RedisExampleWorker;

public class WithBodyFailureHandler : IAsyncEndpointRequestHandler<ExampleRequest, ExampleResponse>
{
    public async Task<MethodResult<ExampleResponse>> HandleAsync(AsyncContext<ExampleRequest> context, CancellationToken token)
    {
        var request = context.Request;

        // Simulate processing delay
        if (request.ProcessingDelaySeconds > 0)
        {
            await Task.Delay(request.ProcessingDelaySeconds * 1000, token);
        }

        // This handler always fails for demonstration purposes
        return MethodResult<ExampleResponse>.Failure($"Failed to process request for {request.Name} - Simulated failure for demonstration");
    }
}
