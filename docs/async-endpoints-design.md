# AsyncEndpoints Initial Design

- It will allow developers to build asynchronous APIs, which processes the request in background
- When user sends the request to Async endpoint it will queue the request and immediately respond with 202 (Accepted) with request id and other necessary metadata
- It will add a new Job record and worker will pick up those jobs as per availability and process them
- Worker will update job meta data (like status) throughout the life time of the job
- It will also retry based on configurations
- There will be an endpoint to check the latest status of the job, it will also show response for completed jobs and exceptions for failed jobs
- It should show results for each runs for particular request
- Request send with request id in header will be used for making request idempotent
- Request send without request id will have a warning to use that header

## Program.cs

```cs
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsRedisStore() // Lowest priority
    .AddAsyncEndpointsEntityFrameworkCoreStore() // Lowest priority
    .AddAsyncEndpointsHandlersFromAssemblyContaining<IAsyncEndpointHandler<,>>() // Does not work with AOT compilation
    .AddAsyncEndpointsHandler<AsyncEndpointHandler<Request, Response>>(); // Alternative way to register handler

var app = builder.Build();

app.MapAsyncPost<Request, Response>("/{resourceId}", async (HttpContext context, Delegate next) => {
    // Optional Handler:
    // For synchronous tasks, for example, request validation.
    // All will be running before storing the task in queue.
    // Once complete we can call next for queueing the request
    await next();
});

await app.RunAsync();
```

## AsyncEndpointHandler.cs

```cs
public class AsyncEndpointHandler<Request, Response>(ILogger<AsyncEndpointHandler<Request, Response>> logger) : IAsyncEndpointHandler<Request, Response>
{
    public async Task<ApiOperationResult<Response>> HandleAsync(AsyncContext<Request> context)
    {
        var resourceId = context.Request.RouteParameters.ResourceId;
        var queryCollection = context.Request.Query;
        var headers = context.Request.Headers;

        logger.LogInformation("Handling request for resourceId {ResourceId}", resourceId);

        var result = await Process(resourceId, queryCollection, headers);

        if (!result.IsSucceeded)
        {
            logger.LogWarning("Request failed: {Errors}", string.Join(", ", result.Errors));
            return ApiOperationResult<Response>.Failure(result.Errors);
        }

        logger.LogInformation("Request processed successfully.");
        return ApiOperationResult<Response>.Success(result.Data);
    }
}
```
