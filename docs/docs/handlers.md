---
sidebar_position: 3
title: Handlers
---

# Handlers

This page explains how to create and implement request handlers for processing asynchronous operations in AsyncEndpoints.

## Handler Interfaces

### With Request Body
Implement `IAsyncEndpointRequestHandler<TRequest, TResponse>` for handlers that process request data:

```csharp
public interface IAsyncEndpointRequestHandler<TRequest, TResponse>
{
    Task<MethodResult<TResponse>> HandleAsync(AsyncContext<TRequest> context, CancellationToken token);
}
```

### Without Request Body
Implement `IAsyncEndpointRequestHandler<TResponse>` for handlers that don't require a request body:

```csharp
public interface IAsyncEndpointRequestHandler<TResponse>
{
    Task<MethodResult<TResponse>> HandleAsync(AsyncContext context, CancellationToken token);
}
```

## Handler Implementation

### With Request Body

```csharp
using AsyncEndpoints.Handlers;
using AsyncEndpoints.Utilities;

public class ProcessDataHandler(ILogger<ProcessDataHandler> logger) 
    : IAsyncEndpointRequestHandler<DataRequest, ProcessResult>
{
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        var request = context.Request;
        
        // Access HTTP context information
        var headers = context.Headers;
        var routeParams = context.RouteParams;
        var queryParams = context.QueryParams;
        
        logger.LogInformation("Processing data request: {Data} with priority {Priority}", 
            request.Data, request.ProcessingPriority);

        try
        {
            // Perform processing logic
            await Task.Delay(TimeSpan.FromSeconds(5), token); // Simulate work
            
            var result = new ProcessResult
            {
                ProcessedData = $"Processed: {request.Data.ToUpper()}",
                ProcessedAt = DateTime.UtcNow,
                CharacterCount = request.Data.Length
            };
            
            logger.LogInformation("Successfully processed request");
            return MethodResult<ProcessResult>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing request: {Data}", request.Data);
            return MethodResult<ProcessResult>.Failure(ex);
        }
    }
}
```

### Without Request Body

```csharp
public class GenerateReportHandler(ILogger<GenerateReportHandler> logger)
    : IAsyncEndpointRequestHandler<ReportResult>
{
    public async Task<MethodResult<ReportResult>> HandleAsync(AsyncContext context, CancellationToken token)
    {
        // Access headers, route parameters, and query parameters
        var headers = context.Headers;
        var routeParams = context.RouteParams;
        var queryParams = context.QueryParams;
        
        // Extract parameters as needed
        var reportId = routeParams.GetValueOrDefault("id", null)?.ToString();
        var format = queryParams.FirstOrDefault(q => q.Key == "format").Value?.FirstOrDefault();
        
        try
        {
            // Process request without body data
            var result = new ReportResult 
            { 
                ReportData = "Generated report data...",
                GeneratedAt = DateTime.UtcNow,
                Format = format ?? "default"
            };
            
            return MethodResult<ReportResult>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating report");
            return MethodResult<ReportResult>.Failure(ex);
        }
    }
}
```

## AsyncContext Usage

The `AsyncContext` and `AsyncContext<TRequest>` provide access to HTTP context information that was available during the original request:

### Accessing HTTP Headers
```csharp
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
{
    var headers = context.Headers;
    
    // Access specific headers
    var authorization = headers.GetValueOrDefault("Authorization", new List<string?>());
    var contentType = headers.GetValueOrDefault("Content-Type", new List<string?>());
    var userAgent = headers.GetValueOrDefault("User-Agent", new List<string?>());
    
    // Process with header information
    return MethodResult<ProcessResult>.Success(new ProcessResult());
}
```

### Accessing Route Parameters
```csharp
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
{
    var routeParams = context.RouteParams;
    
    // Access route parameter values
    var userId = routeParams.GetValueOrDefault("userId", null)?.ToString();
    var action = routeParams.GetValueOrDefault("action", null)?.ToString();
    
    return MethodResult<ProcessResult>.Success(new ProcessResult());
}
```

### Accessing Query Parameters
```csharp
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
{
    var queryParams = context.QueryParams;
    
    // Find specific query parameter
    var formatParam = queryParams.FirstOrDefault(q => q.Key == "format");
    var format = formatParam.Value?.FirstOrDefault();
    
    // Get all query parameters
    foreach (var param in queryParams)
    {
        var paramName = param.Key;
        var paramValues = param.Value;
        // Process each query parameter
    }
    
    return MethodResult<ProcessResult>.Success(new ProcessResult());
}
```

## Error Handling Patterns

### Using MethodResult for Success

```csharp
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
{
    try
    {
        // Process successfully
        var result = new ProcessResult { /* ... */ };
        return MethodResult<ProcessResult>.Success(result);
    }
    catch (Exception ex)
    {
        // Log the error
        logger.LogError(ex, "Unexpected error during processing");
        
        // Return failure with exception
        return MethodResult<ProcessResult>.Failure(ex);
    }
}
```

### Using MethodResult for Validation Errors

```csharp
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
{
    var request = context.Request;
    
    if (string.IsNullOrWhiteSpace(request.Data))
    {
        return MethodResult<ProcessResult>.Failure(
            AsyncEndpointError.FromCode("INVALID_REQUEST", "Data field is required")
        );
    }
    
    // Process with valid data
    var result = new ProcessResult { /* ... */ };
    return MethodResult<ProcessResult>.Success(result);
}
```

## Cancellation Token Usage

Always pass the cancellation token to long-running operations:

```csharp
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
{
    try
    {
        // Pass the token to operations that support cancellation
        await SomeLongRunningOperation(token);
        
        // Use in delays
        await Task.Delay(TimeSpan.FromSeconds(10), token);
        
        // Use with HTTP clients
        var response = await httpClient.GetAsync(url, token);
        
        // Check cancellation manually if needed
        token.ThrowIfCancellationRequested();
        
        return MethodResult<ProcessResult>.Success(new ProcessResult());
    }
    catch (OperationCanceledException) when (token.IsCancellationRequested)
    {
        // Handle cancellation gracefully
        logger.LogInformation("Operation was cancelled");
        return MethodResult<ProcessResult>.Failure(
            AsyncEndpointError.FromCode("OPERATION_CANCELLED", "Operation was cancelled by client")
        );
    }
}
```

## Registration

Register handlers in your `Program.cs` or service configuration:

### With Request Body
```csharp
builder.Services.AddAsyncEndpointHandler<ProcessDataHandler, DataRequest, ProcessResult>("ProcessData");
```

### Without Request Body
```csharp
builder.Services.AddAsyncEndpointHandler<GenerateReportHandler, ReportResult>("GenerateReport");
```

## Performance Considerations

### Memory Management
- Avoid holding large objects in handler instances if using singleton lifetime
- Dispose of resources properly in long-running operations
- Consider using `using` statements for disposable resources

### Concurrency
- Handler instances are registered as scoped services by default
- Each job execution gets a new handler instance
- Dependencies should be thread-safe if they're singleton-scoped

### Async Patterns
- Always use async/await for I/O operations
- Avoid blocking synchronous calls (`.Result`, `.Wait()`, etc.)
- Use `Task.Run()` for CPU-intensive work to avoid blocking the thread pool

## Testing Handlers

Handlers should be designed to be testable:

```csharp
// Example handler designed for testing
public class ProcessDataHandler(
    ILogger<ProcessDataHandler> logger,
    IDataProcessor dataProcessor) // Injectable dependency
    : IAsyncEndpointRequestHandler<DataRequest, ProcessResult>
{
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        var request = context.Request;
        
        try
        {
            var processedData = await dataProcessor.ProcessAsync(request.Data, token);
            
            var result = new ProcessResult
            {
                ProcessedData = processedData,
                ProcessedAt = DateTime.UtcNow,
                CharacterCount = processedData.Length
            };
            
            return MethodResult<ProcessResult>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing request: {Data}", request.Data);
            return MethodResult<ProcessResult>.Failure(ex);
        }
    }
}
```

Handler implementations are the core of your AsyncEndpoints application, containing the business logic that processes your asynchronous operations while maintaining access to the original HTTP context.