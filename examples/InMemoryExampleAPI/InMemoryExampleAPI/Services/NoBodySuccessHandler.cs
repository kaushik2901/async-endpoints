using AsyncEndpoints.Handlers;
using AsyncEndpoints.Utilities;

namespace InMemoryExampleAPI.Services;

public class NoBodySuccessHandler : IAsyncEndpointRequestHandler<string>
{
    public async Task<MethodResult<string>> HandleAsync(AsyncContext context, CancellationToken token)
    {
        // Extract action from query parameters or route parameters
        var action = context.QueryParams
            .FirstOrDefault(x => x.Key == "action").Value?.FirstOrDefault()
                     ?? "default";

        // Simulate processing time based on query parameter
        var delayParam = context.QueryParams
            .FirstOrDefault(x => x.Key == "delay").Value?.FirstOrDefault();
        
        if (int.TryParse(delayParam, out int delaySeconds) && delaySeconds > 0)
        {
            await Task.Delay(delaySeconds * 1000, token);
        }

        var response = $"No-body SUCCESS operation processed successfully with action: {action} at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";
        
        return MethodResult<string>.Success(response);
    }
}