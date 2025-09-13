# AsyncEndpoints Initial Design

## Program.cs

```cs
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsInMemoryStore()
    .AddAsyncEndpointsRedisStore()
    .AddAsyncEndpointsEntityFrameworkCoreStore()
    .AddAsyncEndpointsHandlersFromAssemblyContaining<IAsyncEndpointHandler<,>>() // Does not work with AOT compilation
    .AddAsyncEndpointsHandler<AsyncEndpointHandler<Request, Response>>(); // Alternative way to register handler

var app = builder.Build();

app.MapAsyncPost<Request, Response>("/{resourceId}", async (HttpContext context, Delegate next) => {
    // Optional Handler:
    //      For synchronous tasks, for example, request validation.
    //      All will be running before storing the task in queue.
    //      Once complete we can call next for queueing the request
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
