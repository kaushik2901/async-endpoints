---
sidebar_position: 7
---

# Request Handlers

## Overview

Request handlers are the core of your business logic in AsyncEndpoints. They process requests asynchronously in the background, allowing your API to respond immediately while long-running operations complete in the background.

## Handler Interfaces

### With Request Body

Implement `IAsyncEndpointRequestHandler<TRequest, TResponse>` for handlers that process requests with a body:

```csharp
public class ProcessDataHandler : IAsyncEndpointRequestHandler<DataRequest, ProcessResult>
{
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        var request = context.Request;
        // Process the request and return result
    }
}
```

### Without Request Body

Implement `IAsyncEndpointRequestHandler<TResponse>` for handlers that don't require a request body:

```csharp
public class GenerateReportHandler : IAsyncEndpointRequestHandler<ReportResult>
{
    public async Task<MethodResult<ReportResult>> HandleAsync(AsyncContext context, CancellationToken token)
    {
        // Process without request body
    }
}
```

## Accessing HTTP Context

Handlers have access to the original HTTP context information:

```csharp
public class MyHandler : IAsyncEndpointRequestHandler<InputRequest, OutputResult>
{
    public async Task<MethodResult<OutputResult>> HandleAsync(AsyncContext<InputRequest> context, CancellationToken token)
    {
        var request = context.Request;              // The request body
        var headers = context.Headers;              // HTTP headers from original request
        var routeParams = context.RouteParams;      // Route parameters
        var queryParams = context.QueryParams;      // Query parameters

        // Example: Access Authorization header
        var authHeader = headers["Authorization"]?.FirstOrDefault();

        // Example: Access route parameters
        var resourceId = routeParams["resourceId"]?.ToString();

        // Example: Access query parameters
        var format = queryParams.FirstOrDefault(x => x.Key == "format").Value?.FirstOrDefault();

        // Process the request...
        var result = await ProcessRequest(request, token);
        
        return MethodResult<OutputResult>.Success(result);
    }
}
```

## Return Types

Handlers must return a `MethodResult<T>`:

```csharp
// Success result
return MethodResult<ProcessResult>.Success(result);

// Failure results
return MethodResult<ProcessResult>.Failure("Error message");
return MethodResult<ProcessResult>.Failure(exception);
return MethodResult<ProcessResult>.Failure(error);
```

## MethodResult

The `MethodResult` provides structured error handling:

```csharp
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<InputRequest> context, CancellationToken token)
{
    try
    {
        var result = await PerformProcessing(context.Request);
        
        // Success
        return MethodResult<ProcessResult>.Success(result);
    }
    catch (ValidationException ex)
    {
        // Validation error
        return MethodResult<ProcessResult>.Failure(ex);
    }
    catch (Exception ex)
    {
        // General error
        return MethodResult<ProcessResult>.Failure(ex);
    }
}
```

## Complete Handler Example

Here's a comprehensive handler implementation:

```csharp
using AsyncEndpoints.Handlers;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.Logging;

public class ComplexProcessingHandler : IAsyncEndpointRequestHandler<DataRequest, ProcessResult>
{
    private readonly ILogger<ComplexProcessingHandler> _logger;

    public ComplexProcessingHandler(ILogger<ComplexProcessingHandler> logger)
    {
        _logger = logger;
    }

    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        var request = context.Request;
        var headers = context.Headers;
        var routeParams = context.RouteParams;
        var queryParams = context.QueryParams;

        // Log original request context
        var userId = headers["X-User-Id"]?.FirstOrDefault();
        var resourceId = routeParams["resourceId"]?.ToString();
        var format = queryParams.FirstOrDefault(x => x.Key == "format").Value?.FirstOrDefault();

        _logger.LogInformation("Processing request for user {UserId}, resource {ResourceId}", userId, resourceId);

        try
        {
            // Validate request
            if (string.IsNullOrWhiteSpace(request.Data))
            {
                _logger.LogWarning("Empty data provided for request");
                return MethodResult<ProcessResult>.Failure("Data cannot be empty");
            }

            // Simulate processing with cancellation support
            var progressToken = new Progress<int>(progress => 
                _logger.LogInformation("Processing {Progress}% complete", progress));

            var result = await ProcessDataAsync(request, progressToken, token);

            _logger.LogInformation("Successfully processed request for {UserId}", userId);
            return MethodResult<ProcessResult>.Success(result);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            _logger.LogInformation("Request processing was cancelled for user {UserId}", userId);
            return MethodResult<ProcessResult>.Failure("Request was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request for user {UserId}", userId);
            return MethodResult<ProcessResult>.Failure(ex);
        }
    }

    private async Task<ProcessResult> ProcessDataAsync(DataRequest request, IProgress<int> progress, CancellationToken token)
    {
        // Simulate long-running operation with progress updates
        for (int i = 0; i <= 100; i += 10)
        {
            await Task.Delay(100, token); // Simulate work
            progress?.Report(i);
        }

        return new ProcessResult
        {
            ProcessedData = $"Processed: {request.Data}",
            ProcessedAt = DateTime.UtcNow,
            CharacterCount = request.Data.Length
        };
    }
}
```

## Registration

Register handlers in your `Program.cs`:

```csharp
// With request body
builder.Services.AddAsyncEndpointHandler<ProcessDataHandler, DataRequest, ProcessResult>("ProcessData");

// Without request body
builder.Services.AddAsyncEndpointHandler<GenerateReportHandler, ReportResult>("GenerateReport");
```

## Dependency Injection

Handlers support dependency injection:

```csharp
public class ServiceUsingHandler : IAsyncEndpointRequestHandler<InputRequest, OutputResult>
{
    private readonly IMyService _myService;
    private readonly ILogger<ServiceUsingHandler> _logger;

    public ServiceUsingHandler(IMyService myService, ILogger<ServiceUsingHandler> logger)
    {
        _myService = myService;
        _logger = logger;
    }

    public async Task<MethodResult<OutputResult>> HandleAsync(AsyncContext<InputRequest> context, CancellationToken token)
    {
        var result = await _myService.ProcessAsync(context.Request, token);
        return MethodResult<OutputResult>.Success(result);
    }
}
```

## Async Context Access

Access different parts of the HTTP context:

```csharp
public async Task<MethodResult<OutputResult>> HandleAsync(AsyncContext<InputRequest> context, CancellationToken token)
{
    // Request body
    var requestBody = context.Request;

    // Headers as dictionary of string keys and list of values
    var contentType = context.Headers["Content-Type"]?.FirstOrDefault();
    var userAgent = context.Headers["User-Agent"]?.FirstOrDefault();

    // Route parameters as dictionary of string keys and object values
    var id = context.RouteParams["id"]?.ToString();
    var category = context.RouteParams["category"]?.ToString();

    // Query parameters as enumerable of key-value pairs
    var queryParamValue = context.QueryParams
        .FirstOrDefault(x => x.Key == "paramName")
        .Value?
        .FirstOrDefault();

    // Process with all context information
    // ...
}
```

## Error Handling Best Practices

1. **Structured Errors**: Always return proper `MethodResult` objects
2. **Logging**: Log errors with sufficient context for debugging
3. **Validation**: Validate inputs early in the handler
4. **Cancellations**: Handle `OperationCanceledException` properly
5. **Exceptions**: Catch and convert exceptions to proper error responses

## Performance Considerations

1. **Cancellation Tokens**: Always respect cancellation tokens
2. **Async Operations**: Use async/await throughout your handler
3. **Resource Management**: Dispose of resources properly
4. **Memory**: Be mindful of memory usage for large requests
5. **Connections**: Don't hold connections longer than necessary