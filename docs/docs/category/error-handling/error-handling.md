---
sidebar_position: 2
title: Error Handling
---

# Error Handling

This page details the comprehensive error handling system in AsyncEndpoints, including exception handling in handlers, error propagation, custom error types, and monitoring strategies.

## Overview

AsyncEndpoints provides a robust error handling system that captures, processes, and reports errors at all levels of the async processing pipeline. The system distinguishes between different types of errors and provides appropriate responses and recovery mechanisms.

## Handler Exception Handling

### Using MethodResult for Error Handling

The primary mechanism for error handling in handlers is the `MethodResult<T>` class:

```csharp
public class ProcessDataHandler(ILogger<ProcessDataHandler> logger) 
    : IAsyncEndpointRequestHandler<DataRequest, ProcessResult>
{
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        try
        {
            var result = await ProcessRequestAsync(context.Request, token);
            return MethodResult<ProcessResult>.Success(result);
        }
        catch (ArgumentException ex)
        {
            // Handle validation errors
            logger.LogWarning("Validation error: {Message}", ex.Message);
            return MethodResult<ProcessResult>.Failure(
                AsyncEndpointError.FromCode("VALIDATION_ERROR", ex.Message)
            );
        }
        catch (ExternalServiceException ex)
        {
            // Handle external service errors (likely transient)
            logger.LogError(ex, "External service error during processing");
            return MethodResult<ProcessResult>.Failure(ex);
        }
        catch (Exception ex)
        {
            // Handle unexpected errors
            logger.LogError(ex, "Unexpected error during processing");
            return MethodResult<ProcessResult>.Failure(ex);
        }
    }
}
```

### MethodResult Success and Failure Methods

#### Success Method
```csharp
// Return successful result
return MethodResult<ProcessResult>.Success(new ProcessResult 
{ 
    ProcessedData = processedData,
    ProcessedAt = DateTime.UtcNow
});
```

#### Failure Methods
```csharp
// From exception (most common)
return MethodResult<ProcessResult>.Failure(ex);

// From error code and message
return MethodResult<ProcessResult>.Failure(
    AsyncEndpointError.FromCode("CUSTOM_ERROR", "Something went wrong")
);

// From error code, message, and inner exception
return MethodResult<ProcessResult>.Failure(
    AsyncEndpointError.FromCode("CUSTOM_ERROR", "Something went wrong", ex)
);

// From error object
return MethodResult<ProcessResult>.Failure(new AsyncEndpointError 
{
    Code = "CUSTOM_ERROR",
    Message = "Something went wrong",
    Exception = ExceptionInfo.FromException(ex)
});
```

## Exception Classification

### Built-in Error Types

The system includes error classification to handle different error scenarios appropriately:

```csharp
public class ErrorClassifier
{
    public ErrorType ClassifyError(Exception ex)
    {
        return ex switch
        {
            // Transient errors - should be retried
            TimeoutException => ErrorType.Transient,
            HttpRequestException => ErrorType.Transient,
            ExternalServiceException => ErrorType.Transient,
            
            // Permanent errors - should not be retried
            ArgumentException => ErrorType.Permanent,
            InvalidOperationException => ErrorType.Permanent,
            ValidationException => ErrorType.Permanent,
            
            // Unknown errors - may be retried based on configuration
            _ => ErrorType.Unknown
        };
    }
}
```

### Custom Error Classification

You can implement custom error classification logic:

```csharp
public class CustomErrorClassifier : ErrorClassifier
{
    public override ErrorType ClassifyError(Exception ex)
    {
        // Special handling for custom exception types
        if (ex is DataValidationException)
        {
            return ErrorType.Permanent; // Validation errors are permanent
        }
        
        if (ex is NetworkException networkEx && networkEx.IsTransient)
        {
            return ErrorType.Transient; // Some network errors are transient
        }
        
        return base.ClassifyError(ex); // Fall back to default classification
    }
}
```

## Error Propagation and Reporting

### Error Storage in Jobs

When errors occur, they are preserved in the job record:

```csharp
public async Task<MethodResult> ProcessJobFailure(Guid jobId, AsyncEndpointError error, CancellationToken cancellationToken)
{
    var jobResult = await _jobStore.GetJobById(jobId, cancellationToken);
    if (!jobResult.IsSuccess || jobResult.Data == null)
        return MethodResult.Failure(new AsyncEndpointError("JOB_NOT_FOUND", $"Job {jobId} not found"));

    var job = jobResult.Data;

    // Check if retry is possible based on error type
    if (job.RetryCount < job.MaxRetries && ShouldRetryOnError(error))
    {
        job.IncrementRetryCount();
        var retryDelay = CalculateRetryDelay(job.RetryCount);
        job.SetRetryTime(_dateTimeProvider.UtcNow.Add(retryDelay));
        job.UpdateStatus(JobStatus.Scheduled, _dateTimeProvider);
        job.WorkerId = null; // Release from current worker
        job.Error = error;
    }
    else
    {
        job.SetError(error, _dateTimeProvider);
    }

    return await _jobStore.UpdateJob(job, cancellationToken);
}

private bool ShouldRetryOnError(AsyncEndpointError error)
{
    // Custom logic to determine if an error should trigger a retry
    // This could check the error code, exception type, etc.
    return error.Code != "VALIDATION_ERROR"; // Don't retry validation errors
}
```

### Error Serialization

Errors are serialized for storage and include comprehensive information:

```csharp
public class ExceptionSerializer
{
    public string Serialize(Exception ex)
    {
        var exceptionInfo = ExceptionInfo.FromException(ex);
        return JsonSerializer.Serialize(exceptionInfo, AsyncEndpointsJsonSerializationContext.Default.ExceptionInfo);
    }
    
    public Exception? Deserialize(string serializedException)
    {
        var exceptionInfo = JsonSerializer.Deserialize<ExceptionInfo>(serializedException, AsyncEndpointsJsonSerializationContext.Default.ExceptionInfo);
        return exceptionInfo?.ToException();
    }
}

public class ExceptionInfo
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
    public InnerExceptionInfo? InnerException { get; set; }
    public Dictionary<string, object?> Data { get; set; } = new();
    
    public static ExceptionInfo FromException(Exception ex)
    {
        return new ExceptionInfo
        {
            Type = ex.GetType().FullName ?? ex.GetType().Name,
            Message = ex.Message,
            StackTrace = ex.StackTrace ?? string.Empty,
            InnerException = ex.InnerException != null ? FromException(ex.InnerException) : null,
            Data = new Dictionary<string, object?>(ex.Data.Cast<KeyValuePair<object, object>>()
                .ToDictionary(kvp => kvp.Key.ToString()!, kvp => kvp.Value))
        };
    }
    
    public Exception ToException()
    {
        // Reconstruct exception (simplified implementation)
        var exception = (Exception)Activator.CreateInstance(Type.GetType(Type) ?? typeof(Exception), Message);
        exception.Data.Clear();
        foreach (var kvp in Data)
        {
            exception.Data[kvp.Key] = kvp.Value;
        }
        
        return exception;
    }
}
```

## Custom Error Types and Messages

### Defining Custom Errors

Create specific error types for your application needs:

```csharp
public static class AsyncEndpointErrorCodes
{
    public const string VALIDATION_ERROR = "VALIDATION_ERROR";
    public const string EXTERNAL_SERVICE_TIMEOUT = "EXTERNAL_SERVICE_TIMEOUT";
    public const string INVALID_REQUEST = "INVALID_REQUEST";
    public const string INSUFFICIENT_PERMISSIONS = "INSUFFICIENT_PERMISSIONS";
    public const string RESOURCE_NOT_FOUND = "RESOURCE_NOT_FOUND";
    public const string BUSINESS_RULE_VIOLATION = "BUSINESS_RULE_VIOLATION";
    public const string CIRCUIT_BREAKER_OPEN = "CIRCUIT_BREAKER_OPEN";
}

// Usage in handlers
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
{
    var request = context.Request;
    
    if (string.IsNullOrWhiteSpace(request.Data))
    {
        return MethodResult<ProcessResult>.Failure(
            AsyncEndpointError.FromCode(
                AsyncEndpointErrorCodes.VALIDATION_ERROR, 
                "Data field is required"
            )
        );
    }
    
    if (!await ValidateUserPermissions(context.Headers, request))
    {
        return MethodResult<ProcessResult>.Failure(
            AsyncEndpointError.FromCode(
                AsyncEndpointErrorCodes.INSUFFICIENT_PERMISSIONS, 
                "User does not have permission to process this request"
            )
        );
    }
    
    // Continue with processing
    return MethodResult<ProcessResult>.Success(new ProcessResult());
}
```

### Error Translation and Localization

Handle errors in multiple languages or formats:

```csharp
public class LocalizedErrorService
{
    private readonly Dictionary<string, Dictionary<string, string>> _errorMessages;
    
    public LocalizedErrorService()
    {
        _errorMessages = new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string>
            {
                [AsyncEndpointErrorCodes.VALIDATION_ERROR] = "Validation error occurred",
                [AsyncEndpointErrorCodes.EXTERNAL_SERVICE_TIMEOUT] = "External service is temporarily unavailable"
            },
            ["es"] = new Dictionary<string, string>
            {
                [AsyncEndpointErrorCodes.VALIDATION_ERROR] = "Error de validación",
                [AsyncEndpointErrorCodes.EXTERNAL_SERVICE_TIMEOUT] = "El servicio externo no está disponible temporalmente"
            }
        };
    }
    
    public string GetMessage(string errorCode, string culture = "en")
    {
        var cultureDict = _errorMessages.GetValueOrDefault(culture) ?? _errorMessages["en"];
        return cultureDict.GetValueOrDefault(errorCode, "An error occurred");
    }
}
```

## Graceful Degradation Patterns

### Circuit Breaker Implementation

Implement circuit breaker patterns for external dependencies:

```csharp
public class CircuitBreakerAsyncHandler : IAsyncEndpointRequestHandler<DataRequest, ProcessResult>
{
    private readonly CircuitBreaker _circuitBreaker;
    
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        if (_circuitBreaker.IsOpen)
        {
            // Circuit is open, fail fast
            return MethodResult<ProcessResult>.Failure(
                AsyncEndpointError.FromCode(
                    "CIRCUIT_BREAKER_OPEN", 
                    "Service temporarily unavailable due to high error rate"
                )
            );
        }
        
        try
        {
            var result = await ProcessWithExternalService(context.Request, token);
            _circuitBreaker.RecordSuccess();
            return MethodResult<ProcessResult>.Success(result);
        }
        catch (ExternalServiceException ex)
        {
            _circuitBreaker.RecordFailure();
            
            return MethodResult<ProcessResult>.Failure(
                AsyncEndpointError.FromCode(
                    "EXTERNAL_SERVICE_ERROR",
                    "External service error",
                    ex
                )
            );
        }
    }
}
```

### Fallback Strategies

Implement fallback strategies when primary operations fail:

```csharp
public class FallbackHandler : IAsyncEndpointRequestHandler<DataRequest, ProcessResult>
{
    private readonly IPrimaryService _primaryService;
    private readonly ISecondaryService _secondaryService;
    
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        try
        {
            // Try primary service first
            var result = await _primaryService.ProcessAsync(context.Request, token);
            return MethodResult<ProcessResult>.Success(result);
        }
        catch (PrimaryServiceException primaryEx)
        {
            logger.LogWarning(primaryEx, "Primary service failed, attempting fallback");
            
            try
            {
                // Try secondary service as fallback
                var fallbackResult = await _secondaryService.ProcessAsync(context.Request, token);
                
                logger.LogInformation("Fallback service succeeded after primary failure");
                return MethodResult<ProcessResult>.Success(fallbackResult);
            }
            catch (Exception fallbackEx)
            {
                // Both services failed
                logger.LogError(fallbackEx, "Both primary and secondary services failed");
                
                return MethodResult<ProcessResult>.Failure(
                    AsyncEndpointError.FromCode(
                        "SERVICE_FAILURE",
                        "Both primary and fallback services failed",
                        new AggregateException(primaryEx, fallbackEx)
                    )
                );
            }
        }
    }
}
```

## Logging and Monitoring

### Structured Error Logging

Implement comprehensive error logging with structured data:

```csharp
public class ErrorLoggingHandler : IAsyncEndpointRequestHandler<DataRequest, ProcessResult>
{
    private readonly ILogger<ErrorLoggingHandler> _logger;
    private readonly IErrorTelemetryService _telemetryService;
    
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        var jobId = context.RouteParams.GetValueOrDefault("jobId")?.ToString();
        var userId = context.Headers.GetValueOrDefault("X-User-Id", new List<string?>())?.FirstOrDefault();
        
        using var activity = Telemetry.ActivitySource.StartActivity("ProcessData", ActivityKind.Internal);
        activity?.SetTag("job.id", jobId);
        activity?.SetTag("user.id", userId);
        
        try
        {
            var result = await ProcessRequestAsync(context.Request, token);
            
            _logger.LogInformation(
                "Successfully processed job {JobId} for user {UserId} in {Duration}ms",
                jobId, userId, activity?.Duration.TotalMilliseconds ?? 0);
            
            return MethodResult<ProcessResult>.Success(result);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);
            
            _logger.LogError(
                ex,
                "Error processing job {JobId} for user {UserId}: {ErrorMessage}",
                jobId, userId, ex.Message);
            
            // Send error to telemetry service
            await _telemetryService.RecordError(ex, new Dictionary<string, object?>
            {
                ["jobId"] = jobId,
                ["userId"] = userId,
                ["handler"] = nameof(ErrorLoggingHandler)
            });
            
            return MethodResult<ProcessResult>.Failure(ex);
        }
    }
}
```

### Error Aggregation and Alerting

Aggregate errors for monitoring and alerting:

```csharp
public class ErrorAggregationService
{
    private readonly Dictionary<string, int> _errorCounts = new();
    private readonly object _lock = new();
    
    public void RecordError(string errorCode)
    {
        lock (_lock)
        {
            if (_errorCounts.ContainsKey(errorCode))
            {
                _errorCounts[errorCode]++;
            }
            else
            {
                _errorCounts[errorCode] = 1;
            }
        }
    }
    
    public Dictionary<string, int> GetErrorCounts()
    {
        lock (_lock)
        {
            return new Dictionary<string, int>(_errorCounts);
        }
    }
    
    public bool ShouldAlertForError(string errorCode, int threshold = 10)
    {
        lock (_lock)
        {
            return _errorCounts.GetValueOrDefault(errorCode, 0) >= threshold;
        }
    }
}
```

## Error Recovery Strategies

### Automatic Retry with Backoff

The system automatically handles retries with exponential backoff:

```csharp
// This logic is built into the JobManager
private TimeSpan CalculateRetryDelay(int retryCount)
{
    // Exponential backoff: (2 ^ retryCount) * base delay
    return TimeSpan.FromSeconds(Math.Pow(2, retryCount) * _jobManagerConfiguration.RetryDelayBaseSeconds);
}
```

### Manual Recovery

For critical errors that require manual intervention:

```csharp
public class ManualRecoveryService
{
    public async Task<bool> RetryJobManually(Guid jobId, int maxRetries = 1)
    {
        var jobResult = await _jobStore.GetJobById(jobId, CancellationToken.None);
        if (!jobResult.IsSuccess || jobResult.Data == null)
            return false;

        var job = jobResult.Data;
        
        // Reset job for manual retry
        job.RetryCount = Math.Max(0, job.RetryCount - 1); // Allow one more retry
        job.MaxRetries = Math.Max(job.MaxRetries, job.RetryCount + maxRetries);
        job.Status = JobStatus.Queued;
        job.WorkerId = null;
        job.Error = null; // Clear the previous error
        
        var updateResult = await _jobStore.UpdateJob(job, CancellationToken.None);
        return updateResult.IsSuccess;
    }
}
```

## Testing Error Scenarios

### Unit Testing Error Handling

Test error handling in your handlers:

```csharp
[Fact]
public async Task HandleAsync_WhenExternalServiceFails_ReturnsFailureResult()
{
    // Arrange
    var mockExternalService = new Mock<IExternalService>();
    var error = new ExternalServiceException("Service unavailable");
    mockExternalService
        .Setup(s => s.ProcessAsync(It.IsAny<DataRequest>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(error);
    
    var handler = new ProcessDataHandler(mockExternalService.Object);
    var context = CreateTestContext();
    
    // Act
    var result = await handler.HandleAsync(context, CancellationToken.None);
    
    // Assert
    Assert.False(result.IsSuccess);
    Assert.NotNull(result.Error);
    Assert.Equal(error, result.Error.Exception?.ToException());
}

[Fact]
public async Task HandleAsync_WhenValidationFails_ReturnsValidationFailure()
{
    // Arrange
    var handler = new ProcessDataHandler();
    var invalidRequest = new DataRequest { Data = "" }; // Invalid data
    var context = new AsyncContext<DataRequest>(invalidRequest, new Dictionary<string, List<string?>>(), 
                                               new Dictionary<string, object?>(), 
                                               new List<KeyValuePair<string, List<string?>>>());
    
    // Act
    var result = await handler.HandleAsync(context, CancellationToken.None);
    
    // Assert
    Assert.False(result.IsSuccess);
    Assert.NotNull(result.Error);
    Assert.Equal("VALIDATION_ERROR", result.Error.Code);
}
```

Proper error handling ensures your async processing system is resilient, provides meaningful feedback to users, and can recover gracefully from various failure scenarios.