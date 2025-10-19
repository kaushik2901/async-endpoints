---
sidebar_position: 5
title: Utilities
---

# Utilities

This page provides detailed reference documentation for all AsyncEndpoints utility classes, including their properties, methods, and usage examples.

## AsyncEndpointError

### Class Definition
```csharp
public sealed class AsyncEndpointError
```

### Properties

#### Code
- **Type**: `string`
- **Description**: Gets or sets the error code
- **Access**: Get and set

#### Message
- **Type**: `string`
- **Description**: Gets or sets the error message
- **Access**: Get and set

#### Exception
- **Type**: `ExceptionInfo?`
- **Description**: Gets or sets the exception information
- **Access**: Get and set

### Static Methods

#### FromCode
```csharp
public static AsyncEndpointError FromCode(string code, string message)
```
**Parameters:**
- `code` (`string`): The error code
- `message` (`string`): The error message

**Returns:** A new `AsyncEndpointError` instance

**Description:** Creates an `AsyncEndpointError` with the specified code and message.

```csharp
// Example
var error = AsyncEndpointError.FromCode("VALIDATION_ERROR", "Data validation failed");
```

#### FromCode (With Exception)
```csharp
public static AsyncEndpointError FromCode(string code, string message, Exception exception)
```
**Parameters:**
- `code` (`string`): The error code
- `message` (`string`): The error message
- `exception` (`Exception`): The exception to include in the error

**Returns:** A new `AsyncEndpointError` instance

**Description:** Creates an `AsyncEndpointError` with the specified code, message, and exception information.

```csharp
// Example
var ex = new InvalidOperationException("Processing failed");
var error = AsyncEndpointError.FromCode("PROCESSING_ERROR", "Processing failed", ex);
```

#### FromMessage
```csharp
public static AsyncEndpointError FromMessage(string message)
```
**Parameters:**
- `message` (`string`): The error message

**Returns:** A new `AsyncEndpointError` instance

**Description:** Creates an `AsyncEndpointError` with a generic error code and the specified message.

```csharp
// Example
var error = AsyncEndpointError.FromMessage("An unexpected error occurred");
```

---

## ExceptionInfo

### Class Definition
```csharp
public class ExceptionInfo
```

### Properties

#### Type
- **Type**: `string`
- **Description**: Gets or sets the fully qualified name of the exception type
- **Access:** Get and set

#### Message
- **Type**: `string`
- **Description**: Gets or sets the error message
- **Access:** Get and set

#### StackTrace
- **Type**: `string`
- **Description**: Gets or sets the stack trace string
- **Access:** Get and set

#### InnerException
- **Type**: `InnerExceptionInfo?`
- **Description**: Gets or sets information about the inner exception
- **Access:** Get and set

#### Data
- **Type**: `Dictionary<string, object?>`
- **Description**: Gets or sets additional data associated with the exception
- **Access:** Get and set

### Static Methods

#### FromException
```csharp
public static ExceptionInfo FromException(Exception ex)
```
**Parameters:**
- `ex` (`Exception`): The exception to convert

**Returns:** An `ExceptionInfo` containing information from the exception

**Description:** Creates an `ExceptionInfo` from an exception.

```csharp
// Example
try
{
    throw new InvalidOperationException("Test exception");
}
catch (Exception ex)
{
    var exceptionInfo = ExceptionInfo.FromException(ex);
}
```

### Instance Methods

#### ToException
```csharp
public Exception ToException()
```
**Returns:** An `Exception` object

**Description:** Reconstructs an exception from the information.

```csharp
// Example
var exceptionInfo = ExceptionInfo.FromException(new InvalidOperationException("Test"));
var exception = exceptionInfo.ToException();
```

---

## InnerExceptionInfo

### Class Definition
```csharp
public class InnerExceptionInfo
```

### Properties

#### Type
- **Type**: `string`
- **Description**: Gets or sets the fully qualified name of the inner exception type
- **Access:** Get and set

#### Message
- **Type**: `string`
- **Description**: Gets or sets the inner exception message
- **Access:** Get and set

#### StackTrace
- **Type**: `string`
- **Description**: Gets or sets the inner exception stack trace string
- **Access:** Get and set

### Static Methods

#### FromException
```csharp
public static InnerExceptionInfo FromException(Exception ex)
```
**Parameters:**
- `ex` (`Exception`): The exception to convert

**Returns:** An `InnerExceptionInfo` containing information from the exception

**Description:** Creates an `InnerExceptionInfo` from an exception.

```csharp
// Example
try
{
    throw new InvalidOperationException("Inner exception", new ArgumentException("Inner argument exception"));
}
catch (Exception ex)
{
    var innerExceptionInfo = InnerExceptionInfo.FromException(ex.InnerException);
}
```

---

## MethodResult\&lt;T&gt; Usage Patterns

### Success Pattern
```csharp
// Returning successful results
public async Task<MethodResult<ProcessResult>> ProcessDataAsync(DataRequest request, CancellationToken token)
{
    try
    {
        // Process successfully
        var result = new ProcessResult
        {
            ProcessedData = request.Data.ToUpper(),
            ProcessedAt = DateTime.UtcNow
        };
        
        return MethodResult<ProcessResult>.Success(result);
    }
    catch (Exception ex)
    {
        // Handle error
        return MethodResult<ProcessResult>.Failure(ex);
    }
}
```

### Failure Pattern
```csharp
public async Task<MethodResult<ProcessResult>> ValidateAndProcessAsync(DataRequest request, CancellationToken token)
{
    // Validate input
    if (string.IsNullOrWhiteSpace(request.Data))
    {
        var error = AsyncEndpointError.FromCode("VALIDATION_ERROR", "Data field is required");
        return MethodResult<ProcessResult>.Failure(error);
    }
    
    // Process successfully
    var result = new ProcessResult
    {
        ProcessedData = request.Data.ToUpper(),
        ProcessedAt = DateTime.UtcNow
    };
    
    return MethodResult<ProcessResult>.Success(result);
}
```

### Error Handling with MethodResult
```csharp
public async Task<MethodResult<ProcessResult>> ComplexProcessingAsync(ComplexRequest request, CancellationToken token)
{
    try
    {
        // Perform multiple operations
        var step1Result = await Step1Async(request, token);
        if (!step1Result.IsSuccess)
        {
            return MethodResult<ProcessResult>.Failure(step1Result.Error!);
        }
        
        var step2Result = await Step2Async(step1Result.Data, token);
        if (!step2Result.IsSuccess)
        {
            return MethodResult<ProcessResult>.Failure(step2Result.Error!);
        }
        
        // Final result
        return MethodResult<ProcessResult>.Success(step2Result.Data);
    }
    catch (Exception ex)
    {
        return MethodResult<ProcessResult>.Failure(ex);
    }
}
```

---

## Serialization Utilities

### AsyncEndpointsJsonSerializationContext

This is a generated JSON serialization context that provides efficient serialization for AsyncEndpoints types.

```csharp
// Example usage for custom serialization
var options = new JsonSerializerOptions
{
    TypeInfoResolver = AsyncEndpointsJsonSerializationContext.Default
};

var serialized = JsonSerializer.Serialize(job, AsyncEndpointsJsonSerializationContext.Default.Job);
var deserialized = JsonSerializer.Deserialize<Job>(serialized, AsyncEndpointsJsonSerializationContext.Default.Job);
```

---

## Helper Classes

### NoBodyRequest

A special request type used for handlers that don't require a request body.

```csharp
public sealed class NoBodyRequest
{
    private static readonly NoBodyRequest _instance = new();
    
    private NoBodyRequest() { }
    
    public static NoBodyRequest CreateInstance() => _instance;
}
```

**Usage:**
```csharp
// Used in handlers without body
public class GenerateReportHandler : IAsyncEndpointRequestHandler<ReportResult>
{
    public async Task<MethodResult<ReportResult>> HandleAsync(AsyncContext context, CancellationToken token)
    {
        // Process without request body
        var result = new ReportResult
        {
            ReportData = "Generated report...",
            GeneratedAt = DateTime.UtcNow
        };
        
        return MethodResult<ReportResult>.Success(result);
    }
}
```

---

## Error Classifier

### ErrorType Enum

```csharp
public enum ErrorType
{
    Permanent,  // Should not be retried
    Transient,  // Can be retried
    Unknown     // Retry behavior unknown
}
```

### ErrorClassifier

An abstract class for determining error types.

```csharp
public abstract class ErrorClassifier
{
    public abstract ErrorType ClassifyError(Exception ex);
    
    protected virtual ErrorType ClassifySystemError(Exception ex)
    {
        return ex switch
        {
            TimeoutException => ErrorType.Transient,
            HttpRequestException => ErrorType.Transient,
            InvalidOperationException => ErrorType.Permanent,
            ArgumentException => ErrorType.Permanent,
            _ => ErrorType.Unknown
        };
    }
}
```

**Example Implementation:**
```csharp
public class CustomErrorClassifier : ErrorClassifier
{
    public override ErrorType ClassifyError(Exception ex)
    {
        return ex switch
        {
            DataValidationException => ErrorType.Permanent,
            NetworkException netEx when netEx.IsTransient => ErrorType.Transient,
            _ => ClassifySystemError(ex)
        };
    }
}
```

---

## Response Utilities

### ResponseDefaults

Provides default response creation methods.

```csharp
public static class ResponseDefaults
{
    public static Task<IResult> CreateJobSubmittedResponse(Job job, HttpContext context)
    {
        return Task.FromResult<IResult>(Results.Accepted($"/jobs/{job.Id}", job));
    }
    
    public static Task<IResult> CreateJobStatusResponse(MethodResult<Job> jobResult, HttpContext context)
    {
        if (jobResult.IsSuccess && jobResult.Data != null)
        {
            return Task.FromResult<IResult>(Results.Ok(jobResult.Data));
        }
        
        return Task.FromResult<IResult>(Results.NotFound());
    }
    
    public static Task<IResult> CreateJobSubmissionErrorResponse(AsyncEndpointError? error, HttpContext context)
    {
        return Task.FromResult<IResult>(Results.Problem(
            title: "Job Submission Failed",
            detail: error?.Message,
            statusCode: 500
        ));
    }
    
    public static Task<IResult> CreateExceptionResponse(Exception exception, HttpContext context)
    {
        return Task.FromResult<IResult>(Results.Problem(
            title: "Internal Server Error",
            detail: "An error occurred while processing the request",
            statusCode: 500
        ));
    }
}
```

---

## DateTime Provider

### IDateTimeProvider Interface

```csharp
public interface IDateTimeProvider
{
    DateTimeOffset DateTimeOffsetNow { get; }
    DateTime UtcNow { get; }
}
```

### DateTimeProvider Implementation

```csharp
public class DateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset DateTimeOffsetNow => DateTimeOffset.UtcNow;
    public DateTime UtcNow => DateTime.UtcNow;
}
```

**Usage in Job Creation:**
```csharp
var dateTimeProvider = new DateTimeProvider();
var job = Job.Create(
    Guid.NewGuid(), 
    "ProcessData", 
    serializedPayload,
    headers,
    routeParams,
    queryParams,
    dateTimeProvider
);
```

These utilities provide comprehensive error handling, serialization, and helper functionality that support the core AsyncEndpoints functionality.