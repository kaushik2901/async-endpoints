using AsyncEndpoints.Handlers;
using AsyncEndpoints.Utilities;
using RedisExampleCore;

namespace RedisExampleWorker;

public class WithBodySuccessHandler : IAsyncEndpointRequestHandler<ExampleRequest, ExampleResponse>
{
    public async Task<MethodResult<ExampleResponse>> HandleAsync(AsyncContext<ExampleRequest> context, CancellationToken token)
    {
        var request = context.Request;

        // Simulate processing with delay
        if (request.ProcessingDelaySeconds > 0)
        {
            await Task.Delay(request.ProcessingDelaySeconds * 1000, token);
        }

        var response = new ExampleResponse
        {
            Id = request.Id,
            Message = $"Successfully processed request for {request.Name}",
            Status = 200,
            ProcessedAt = DateTime.UtcNow,
            OriginalName = request.Name
        };

        return MethodResult<ExampleResponse>.Success(response);
    }
}
