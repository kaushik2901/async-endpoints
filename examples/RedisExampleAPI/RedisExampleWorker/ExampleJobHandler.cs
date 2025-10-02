using AsyncEndpoints.Handlers;
using AsyncEndpoints.Utilities;
using RedisExampleCore;

namespace RedisExampleWorker;

public class ExampleJobHandler : IAsyncEndpointRequestHandler<ExampleJobRequest, ExampleJobResponse>
{
    public async Task<MethodResult<ExampleJobResponse>> HandleAsync(AsyncContext<ExampleJobRequest> context, CancellationToken token)
    {
        var request = context.Request;

        // Simulate some work with a delay
        if (request.DelayInSeconds > 0)
        {
            await Task.Delay(request.DelayInSeconds * 1000, token);
        }

		var response = new ExampleJobResponse
        {
            Status = "Processed",
            ProcessedAt = DateTime.UtcNow,
            OriginalMessage = request.Message,
        };

        return MethodResult<ExampleJobResponse>.Success(response);
    }
}
