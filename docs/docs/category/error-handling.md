---
sidebar_position: 12
---

# Error Handling & Troubleshooting

## Overview

AsyncEndpoints provides a comprehensive error handling system that allows you to gracefully handle failures, implement retry mechanisms, and provide meaningful error responses to clients.

## Error Handling Architecture

### MethodResult Pattern

AsyncEndpoints uses the `MethodResult<T>` pattern for structured error handling:

```csharp
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<InputRequest> context, CancellationToken token)
{
    try
    {
        // Process the request
        var result = await ProcessRequest(context.Request, token);
        return MethodResult<ProcessResult>.Success(result);
    }
    catch (ValidationException ex)
    {
        // Validation errors
        return MethodResult<ProcessResult>.Failure(ex);
    }
    catch (ExternalServiceException ex)
    {
        // External service errors (likely retryable)
        return MethodResult<ProcessResult>.Failure(AsyncEndpointError.FromCode("EXTERNAL_SERVICE_ERROR", ex.Message, ex));
    }
    catch (Exception ex)
    {
        // General errors
        return MethodResult<ProcessResult>.Failure(ex);
    }
}
```

### AsyncEndpointError

The `AsyncEndpointError` class provides structured error information:

```csharp
// Create from exception
var error = AsyncEndpointError.FromException(exception);

// Create from code and message
var error = AsyncEndpointError.FromCode("VALIDATION_ERROR", "Invalid input data");

// Create from message only
var error = AsyncEndpointError.FromMessage("Processing failed");
```

## Retry Mechanism

### Configuration

Configure retry behavior in your application:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.JobManagerConfiguration.DefaultMaxRetries = 5;              // Maximum retry attempts
    options.JobManagerConfiguration.RetryDelayBaseSeconds = 3.0;        // Base for exponential backoff
});
```

### How Retries Work

1. When a handler returns a failure result, the system checks if retries remain
2. If retries are available, the retry count is incremented
3. An exponential backoff delay is calculated: `2^retryCount * baseDelay`
4. The job is scheduled for retry at a future time
5. The job status changes to `Scheduled`
6. After the delay, the job returns to `Queued` status for another attempt

### Retry Examples

#### With Custom Error Classification

```csharp
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<InputRequest> context, CancellationToken token)
{
    try
    {
        var result = await ProcessRequest(context.Request, token);
        return MethodResult<ProcessResult>.Success(result);
    }
    catch (TransientException ex)
    {
        // Transient errors should be retried
        return MethodResult<ProcessResult>.Failure(AsyncEndpointError.FromCode("TRANSIENT_ERROR", ex.Message, ex));
    }
    catch (PermanentException ex)
    {
        // Permanent errors should not be retried
        return MethodResult<ProcessResult>.Failure(AsyncEndpointError.FromCode("PERMANENT_ERROR", ex.Message));
    }
}
```

## Custom Error Responses

Customize how errors are returned to clients:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.ResponseConfigurations.ExceptionResponseFactory = async (exception, context) =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(exception, "Unhandled exception in async endpoint");
        
        return Results.Problem(
            title: "Processing Error",
            detail: "An error occurred while processing your request",
            statusCode: 500
        );
    };
    
    options.ResponseConfigurations.JobStatusResponseFactory = async (jobResult, context) =>
    {
        if (jobResult.IsSuccess && jobResult.Data != null)
        {
            var job = jobResult.Data;
            
            if (job.Status == JobStatus.Failed)
            {
                return Results.Problem(new ProblemDetails
                {
                    Title: "Job Failed",
                    Detail: job.Error?.Message,
                    Status = 500,
                    Extensions = 
                    {
                        ["jobId"] = job.Id,
                        ["retryCount"] = job.RetryCount,
                        ["maxRetries"] = job.MaxRetries
                    }
                });
            }
            
            return Results.Ok(job);
        }
        
        return Results.NotFound("Job not found");
    };
});
```

## Common Error Scenarios

### 1. Validation Errors

Handle request validation before or during processing:

```csharp
// In validation middleware
app.MapAsyncPost<InputRequest>("Process", "/api/process",
    async (HttpContext context, InputRequest request, CancellationToken token) =>
    {
        if (string.IsNullOrWhiteSpace(request.Data))
        {
            return Results.BadRequest("Data is required");
        }
        
        if (request.Priority < 1 || request.Priority > 5)
        {
            return Results.BadRequest("Priority must be between 1 and 5");
        }
        
        return null; // Continue processing
    });

// Or in the handler
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<InputRequest> context, CancellationToken token)
{
    if (string.IsNullOrWhiteSpace(context.Request.Data))
    {
        return MethodResult<ProcessResult>.Failure("Data is required");
    }
    
    // Process...
}
```

### 2. External Service Failures

Handle failures when calling external services:

```csharp
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<InputRequest> context, CancellationToken token)
{
    try
    {
        var externalResult = await _externalService.ProcessAsync(context.Request, token);
        
        // Check if external service indicates a retryable error
        if (externalResult.IsRetryable)
        {
            return MethodResult<ProcessResult>.Failure(AsyncEndpointError.FromCode(
                "EXTERNAL_SERVICE_RETRYABLE", 
                "External service temporarily unavailable"));
        }
        
        return MethodResult<ProcessResult>.Success(externalResult.Data);
    }
    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
    {
        // Service is temporarily unavailable, should retry
        return MethodResult<ProcessResult>.Failure(AsyncEndpointError.FromException(ex));
    }
    catch (TimeoutException ex)
    {
        // Timeout might be retryable
        return MethodResult<ProcessResult>.Failure(AsyncEndpointError.FromCode("TIMEOUT", ex.Message, ex));
    }
}
```

### 3. Serialization Errors

Handle errors in data serialization/deserialization:

```csharp
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<InputRequest> context, CancellationToken token)
{
    try
    {
        // This is handled automatically by the framework for input
        // But you might need custom serialization for complex results
        var result = await ProcessRequest(context.Request, token);
        
        // Verify result can be serialized
        var json = JsonSerializer.Serialize(result);
        
        return MethodResult<ProcessResult>.Success(result);
    }
    catch (JsonException ex)
    {
        return MethodResult<ProcessResult>.Failure(AsyncEndpointError.FromCode(
            "SERIALIZATION_ERROR", 
            $"Failed to serialize result: {ex.Message}"));
    }
}
```

## Troubleshooting

### Debugging Job Failures

Enable detailed logging to troubleshoot job failures:

```csharp
// In Program.cs
builder.Logging.AddFilter("AsyncEndpoints", LogLevel.Debug);

// Or in appsettings.json
{
  "Logging": {
    "LogLevel": {
      "AsyncEndpoints": "Debug",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

### Common Issues and Solutions

#### Issue: Jobs Not Processing
**Symptoms:** Jobs are queued but never processed
**Solutions:**
- Verify `AddAsyncEndpointsWorker()` is called in `Program.cs`
- Check that the background service is running (look for logs)
- Ensure handlers are properly registered
- Verify storage is working correctly

#### Issue: High Memory Usage
**Symptoms:** Application memory usage increases over time
**Solutions:**
- If using in-memory store, implement proper cleanup
- Configure appropriate queue size limits: `MaximumQueueSize`
- Monitor and potentially reduce batch sizes
- Check for memory leaks in your handlers

#### Issue: Job Timeouts
**Symptoms:** Jobs marked as failed due to timeout
**Solutions:**
- Increase `JobTimeoutMinutes` in worker configuration
- Optimize your handler code to run faster
- Consider breaking large jobs into smaller ones
- Monitor the actual processing time of your jobs

#### Issue: Duplicate Processing
**Symptoms:** Same job processed multiple times
**Solutions:**
- Ensure you're using Redis store in multi-instance deployments
- Verify job claiming logic is working (should be automatic with Redis)
- Check for multiple instances without distributed coordination

#### Issue: Retry Limit Exceeded
**Symptoms:** Jobs failing after multiple retries
**Solutions:**
- Analyze the root cause of failures
- Increase retry attempts if appropriate: `DefaultMaxRetries`
- Implement better error handling in handlers
- Consider different error handling for different error types

### Performance Monitoring

Monitor these key metrics:

```csharp
// Example: Custom logging for performance monitoring
public class PerformanceMonitoringHandler : IAsyncEndpointRequestHandler<InputRequest, OutputResult>
{
    private readonly ILogger<PerformanceMonitoringHandler> _logger;

    public PerformanceMonitoringHandler(ILogger<PerformanceMonitoringHandler> logger)
    {
        _logger = logger;
    }

    public async Task<MethodResult<OutputResult>> HandleAsync(AsyncContext<InputRequest> context, CancellationToken token)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting job {JobId} at {StartTime}", context.RouteParams["jobId"], startTime);

        try
        {
            var result = await ProcessRequest(context.Request, token);
            
            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Job {JobId} completed in {Duration}ms", context.RouteParams["jobId"], duration.TotalMilliseconds);
            
            return MethodResult<OutputResult>.Success(result);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "Job {JobId} failed after {Duration}ms", context.RouteParams["jobId"], duration.TotalMilliseconds);
            throw;
        }
    }
}
```

### Health Checks

Implement health checks for your async processing:

```csharp
// Custom health check
public class AsyncEndpointsHealthCheck : IHealthCheck
{
    private readonly IJobStore _jobStore;

    public AsyncEndpointsHealthCheck(IJobStore jobStore)
    {
        _jobStore = jobStore;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Test storage availability
            var testJob = await _jobStore.GetJobById(Guid.NewGuid(), cancellationToken);
            
            // Or perform any other relevant health check
            return HealthCheckResult.Healthy("AsyncEndpoints storage is accessible");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"AsyncEndpoints storage is not accessible: {ex.Message}");
        }
    }
}

// Register the health check
builder.Services.AddHealthChecks()
    .AddCheck<AsyncEndpointsHealthCheck>("async-endpoints");
```

## Best Practices

1. **Always Handle Exceptions**: Wrap your handler logic in try-catch blocks
2. **Use Specific Error Codes**: Use meaningful error codes for better debugging
3. **Log Sufficient Context**: Include job IDs and relevant data in logs
4. **Distinguish Error Types**: Separate transient from permanent errors
5. **Test Error Scenarios**: Include error handling in your tests
6. **Monitor Retry Patterns**: Watch for jobs that repeatedly fail
7. **Implement Circuit Breakers**: Prevent system overload during failures
8. **Validate Inputs Early**: Fail fast with validation errors
9. **Provide User Feedback**: Give meaningful error messages to API consumers
10. **Use Appropriate Retries**: Don't retry operations that will never succeed

By following these patterns and practices, you can build robust async endpoints that handle errors gracefully and provide a good experience for both developers and end users.