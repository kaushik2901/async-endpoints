---
sidebar_position: 4
title: Core Models
---

# Core Models

This page provides detailed reference documentation for all AsyncEndpoints core models, including their properties, methods, and usage examples.

## Job

### Class Definition
```csharp
public sealed class Job(DateTimeOffset currentTime)
```

### Constructors

#### Job() (Parameterless)
```csharp
public Job() : this(DateTimeOffset.UtcNow)
```
**Description:** Initializes a new instance of the `Job` class with the current time.

#### Job(DateTimeOffset)
```csharp
public Job(DateTimeOffset currentTime)
```
**Description:** Initializes a new instance of the `Job` class with a specific time.

### Properties

#### Id
- **Type**: `Guid`
- **Description**: Gets the unique identifier of the job
- **Access**: Get only
- **Default**: `Guid.NewGuid()`

#### Name
- **Type**: `string`
- **Description**: Gets or sets the name of the job
- **Default**: `string.Empty`

#### Status
- **Type**: `JobStatus`
- **Description**: Gets or sets the current status of the job
- **Default**: `JobStatus.Queued`

#### Headers
- **Type**: `Dictionary<string, List<string?>>`
- **Description**: Gets or sets the collection of HTTP headers associated with the job
- **Default**: Empty dictionary

#### RouteParams
- **Type**: `Dictionary<string, object?>`
- **Description**: Gets or sets the route parameters associated with the job
- **Default**: Empty dictionary

#### QueryParams
- **Type**: `List<KeyValuePair<string, List<string?>>>`
- **Description**: Gets or sets the query parameters associated with the job
- **Default**: Empty list

#### Payload
- **Type**: `string`
- **Description**: Gets the payload data for the job
- **Access**: Get only
- **Default**: `string.Empty`

#### Result
- **Type**: `string?`
- **Description**: Gets or sets the result of the job execution, if successful
- **Default**: `null`

#### Error
- **Type**: `AsyncEndpointError?`
- **Description**: Gets or sets the error details if the job failed
- **Default**: `null`

#### RetryCount
- **Type**: `int`
- **Description**: Gets or sets the number of times the job has been retried
- **Default**: `0`

#### MaxRetries
- **Type**: `int`
- **Description**: Gets or sets the maximum number of retries allowed for the job
- **Default**: `AsyncEndpointsConstants.MaximumRetries`

#### RetryDelayUntil
- **Type**: `DateTime?`
- **Description**: Gets or sets the time until which the job is scheduled for retry
- **Default**: `null`

#### WorkerId
- **Type**: `Guid?`
- **Description**: Gets or sets the ID of the worker currently processing this job, if any
- **Default**: `null`

#### CreatedAt
- **Type**: `DateTimeOffset`
- **Description**: Gets or sets the date and time when the job was created
- **Default**: Constructor time

#### StartedAt
- **Type**: `DateTimeOffset?`
- **Description**: Gets or sets the date and time when the job processing started, if applicable
- **Default**: `null`

#### CompletedAt
- **Type**: `DateTimeOffset?`
- **Description**: Gets or sets the date and time when the job processing completed, if applicable
- **Default**: `null`

#### LastUpdatedAt
- **Type**: `DateTimeOffset`
- **Description**: Gets or sets the date and time when the job was last updated
- **Default**: Constructor time

#### IsCanceled
- **Type**: `bool`
- **Description**: Gets a value indicating whether the job has been canceled
- **Access**: Get only

### Static Methods

#### Create (With Payload Only)
```csharp
public static Job Create(Guid id, string name, string payload, IDateTimeProvider dateTimeProvider)
```
**Parameters:**
- `id` (`Guid`): The unique identifier for the job
- `name` (`string`): The name of the job
- `payload` (`string`): The payload data for the job
- `dateTimeProvider` (`IDateTimeProvider`): Provider for current date and time

**Returns:** A new `Job` instance

**Description:** Creates a new job with the specified parameters.

```csharp
// Example
var job = Job.Create(Guid.NewGuid(), "ProcessData", serializedPayload, dateTimeProvider);
```

#### Create (With HTTP Context)
```csharp
public static Job Create(
    Guid id,
    string name,
    string payload,
    Dictionary<string, List<string?>> headers,
    Dictionary<string, object?> routeParams,
    List<KeyValuePair<string, List<string?>>> queryParams,
    IDateTimeProvider dateTimeProvider)
```
**Parameters:**
- `id` (`Guid`): The unique identifier for the job
- `name` (`string`): The name of the job
- `payload` (`string`): The payload data for the job
- `headers` (`Dictionary<string, List<string?>>`): The HTTP headers associated with the original request
- `routeParams` (`Dictionary<string, object?>`): The route parameters associated with the original request
- `queryParams` (`List<KeyValuePair<string, List<string?>>>`): The query parameters associated with the original request
- `dateTimeProvider` (`IDateTimeProvider`): Provider for current date and time

**Returns:** A new `Job` instance

**Description:** Creates a new job with the specified parameters including HTTP context information.

```csharp
// Example
var job = Job.Create(
    Guid.NewGuid(), 
    "ProcessData", 
    serializedPayload,
    httpHeaders,
    routeParameters,
    queryParameters,
    dateTimeProvider
);
```

### Instance Methods

#### UpdateStatus
```csharp
public void UpdateStatus(JobStatus status, IDateTimeProvider dateTimeProvider)
```
**Parameters:**
- `status` (`JobStatus`): The new status to set for the job
- `dateTimeProvider` (`IDateTimeProvider`): Provider for current date and time

**Description:** Updates the status of the job and updates the last updated timestamp.

```csharp
// Example
job.UpdateStatus(JobStatus.InProgress, dateTimeProvider);
```

#### SetResult
```csharp
public void SetResult(string result, IDateTimeProvider dateTimeProvider)
```
**Parameters:**
- `result` (`string`): The result of the job execution
- `dateTimeProvider` (`IDateTimeProvider`): Provider for current date and time

**Description:** Sets the result of the job and updates the status to completed.

```csharp
// Example
job.SetResult(serializedResult, dateTimeProvider);
```

#### SetError
```csharp
public void SetError(AsyncEndpointError error, IDateTimeProvider dateTimeProvider)
```
**Parameters:**
- `error` (`AsyncEndpointError`): The error that occurred during job execution
- `dateTimeProvider` (`IDateTimeProvider`): Provider for current date and time

**Description:** Sets the error details for the job and updates the status to failed.

```csharp
// Example
var error = AsyncEndpointError.FromCode("PROCESSING_ERROR", "Failed to process data");
job.SetError(error, dateTimeProvider);
```

#### SetError (String)
```csharp
public void SetError(string error, IDateTimeProvider dateTimeProvider)
```
**Parameters:**
- `error` (`string`): The error message that occurred during job execution
- `dateTimeProvider` (`IDateTimeProvider`): Provider for current date and time

**Description:** Sets the error details for the job and updates the status to failed.

```csharp
// Example
job.SetError("Failed to process data", dateTimeProvider);
```

#### IncrementRetryCount
```csharp
public void IncrementRetryCount()
```
**Description:** Increments the retry count for the job.

```csharp
// Example
job.IncrementRetryCount();
```

#### SetRetryTime
```csharp
public void SetRetryTime(DateTime delayUntil)
```
**Parameters:**
- `delayUntil` (`DateTime`): The time until which the job is scheduled for retry

**Description:** Sets the retry delay time for the job.

```csharp
// Example
job.SetRetryTime(DateTime.UtcNow.AddMinutes(5));
```

#### CreateCopy
```csharp
public Job CreateCopy(
    JobStatus? status = null,
    Guid? workerId = null,
    DateTimeOffset? startedAt = null,
    DateTimeOffset? completedAt = null,
    DateTimeOffset? lastUpdatedAt = null,
    string? result = null,
    AsyncEndpointError? error = null,
    int? retryCount = null,
    DateTime? retryDelayUntil = null,
    IDateTimeProvider? dateTimeProvider = null)
```
**Parameters:**
- `status` (`JobStatus?`): Optional new status for the job
- `workerId` (`Guid?`): Optional new worker ID for the job
- `startedAt` (`DateTimeOffset?`): Optional new started time for the job
- `completedAt` (`DateTimeOffset?`): Optional new completed time for the job
- `lastUpdatedAt` (`DateTimeOffset?`): Optional new last updated time for the job
- `result` (`string?`): Optional new result for the job
- `error` (`AsyncEndpointError?`): Optional new error for the job
- `retryCount` (`int?`): Optional new retry count for the job
- `retryDelayUntil` (`DateTime?`): Optional new retry delay time for the job
- `dateTimeProvider` (`IDateTimeProvider?`): Provider for current date and time

**Returns:** A new job instance with copied properties and any specified updates

**Description:** Creates a deep copy of the current job with updated properties.

```csharp
// Example
var updatedJob = job.CreateCopy(
    status: JobStatus.InProgress,
    workerId: workerId,
    startedAt: DateTimeOffset.UtcNow
);
```

---

## JobStatus

### Enum Definition
```csharp
public enum JobStatus
```

### Values

#### Queued
- **Value:** `0`
- **Description:** Job created and waiting for processing

#### Scheduled
- **Value:** `1`
- **Description:** Job scheduled for delayed execution (with retry delays)

#### InProgress
- **Value:** `2`
- **Description:** Currently being processed by a worker

#### Completed
- **Value:** `3`
- **Description:** Successfully completed with result available

#### Failed
- **Value:** `4`
- **Description:** Failed after all retry attempts exhausted

#### Canceled
- **Value:** `5`
- **Description:** Explicitly canceled before completion

### Example
```csharp
// Example usage
Job job = new Job();
job.UpdateStatus(JobStatus.InProgress, dateTimeProvider);

switch (job.Status)
{
    case JobStatus.Queued:
        Console.WriteLine("Job is queued for processing");
        break;
    case JobStatus.Completed:
        Console.WriteLine("Job completed successfully");
        break;
    case JobStatus.Failed:
        Console.WriteLine($"Job failed: {job.Error?.Message}");
        break;
}
```

---

## AsyncContext

### Class Definition
```csharp
public class AsyncContext(
    IDictionary<string, List<string?>> headers,
    IDictionary<string, object?> routeParams,
    IEnumerable<KeyValuePair<string, List<string?>>> query)
```

### Constructors
```csharp
public AsyncContext(
    IDictionary<string, List<string?>> headers,
    IDictionary<string, object?> routeParams,
    IEnumerable<KeyValuePair<string, List<string?>>> query)
```

### Parameters
- `headers` (`IDictionary<string, List<string?>>`): The HTTP headers from the original request
- `routeParams` (`IDictionary<string, object?>`): The route parameters from the original request
- `query` (`IEnumerable<KeyValuePair<string, List<string?>>>`): The query parameters from the original request

### Properties

#### Headers
- **Type**: `IDictionary<string, List<string?>>`
- **Description**: Gets the HTTP headers from the original request
- **Access**: Get only

#### RouteParams
- **Type**: `IDictionary<string, object?>`
- **Description**: Gets or sets the route parameters from the original request
- **Access**: Get and set

#### QueryParams
- **Type**: `IEnumerable<KeyValuePair<string, List<string?>>>`
- **Description**: Gets the query parameters from the original request
- **Access**: Get only

### Example
```csharp
// Example usage in a handler
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
{
    var headers = context.Headers;
    var routeParams = context.RouteParams;
    var queryParams = context.QueryParams;
    
    // Access specific header
    var userId = headers.GetValueOrDefault("X-User-Id", new List<string?>())?.FirstOrDefault();
    
    // Access route parameter
    var action = routeParams.GetValueOrDefault("action")?.ToString();
    
    // Access query parameter
    var format = queryParams.FirstOrDefault(q => q.Key == "format").Value?.FirstOrDefault();
    
    return MethodResult<ProcessResult>.Success(new ProcessResult());
}
```

---

## AsyncContext\&lt;TRequest&gt;

### Class Definition
```csharp
public sealed class AsyncContext<TRequest>(
    TRequest request,
    IDictionary<string, List<string?>> headers,
    IDictionary<string, object?> routeParams,
    IEnumerable<KeyValuePair<string, List<string?>>> query) : AsyncContext(headers, routeParams, query)
```

### Constructors
```csharp
public AsyncContext<TRequest>(
    TRequest request,
    IDictionary<string, List<string?>> headers,
    IDictionary<string, object?> routeParams,
    IEnumerable<KeyValuePair<string, List<string?>>> query)
```

### Parameters
- `request` (`TRequest`): The original request object
- `headers` (`IDictionary<string, List<string?>>`): The HTTP headers from the original request
- `routeParams` (`IDictionary<string, object?>`): The route parameters from the original request
- `query` (`IEnumerable<KeyValuePair<string, List<string?>>>`): The query parameters from the original request

### Properties

#### Request
- **Type**: `TRequest`
- **Description**: Gets the original request object
- **Access**: Get only

### Example
```csharp
// Example usage
public class ProcessDataHandler : IAsyncEndpointRequestHandler<DataRequest, ProcessResult>
{
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        var request = context.Request; // Access the typed request object
        var headers = context.Headers; // Access HTTP context information
        var routeParams = context.RouteParams;
        var queryParams = context.QueryParams;
        
        // Process with both the request object and HTTP context
        var result = new ProcessResult
        {
            ProcessedData = request.Data.ToUpper(),
            ProcessedAt = DateTime.UtcNow
        };
        
        return MethodResult<ProcessResult>.Success(result);
    }
}
```

---

## MethodResult\&lt;T&gt;

### Class Definition
```csharp
public class MethodResult<T>
```

### Properties

#### IsSuccess
- **Type**: `bool`
- **Description**: Gets whether the operation was successful
- **Access**: Get only

#### Data
- **Type**: `T`
- **Description**: Gets the result data if successful, otherwise null
- **Access**: Get only

#### Error
- **Type**: `AsyncEndpointError?`
- **Description**: Gets the error information if the operation failed, otherwise null
- **Access**: Get only

### Static Methods

#### Success
```csharp
public static MethodResult<T> Success(T data)
```
**Parameters:**
- `data` (`T`): The result data

**Returns:** A `MethodResult<T>` indicating success

**Description:** Creates a successful result with the specified data.

```csharp
// Example
var result = MethodResult<ProcessResult>.Success(new ProcessResult());
```

#### Failure (Exception)
```csharp
public static MethodResult<T> Failure(Exception exception)
```
**Parameters:**
- `exception` (`Exception`): The exception that occurred

**Returns:** A `MethodResult<T>` indicating failure

**Description:** Creates a failed result with the specified exception.

```csharp
// Example
var result = MethodResult<ProcessResult>.Failure(new InvalidOperationException("Processing failed"));
```

#### Failure (AsyncEndpointError)
```csharp
public static MethodResult<T> Failure(AsyncEndpointError error)
```
**Parameters:**
- `error` (`AsyncEndpointError`): The error information

**Returns:** A `MethodResult<T>` indicating failure

**Description:** Creates a failed result with the specified error.

```csharp
// Example
var error = AsyncEndpointError.FromCode("PROCESSING_ERROR", "Processing failed");
var result = MethodResult<ProcessResult>.Failure(error);
```

---

## MethodResult (Non-generic)

### Class Definition
```csharp
public class MethodResult
```

### Properties

#### IsSuccess
- **Type**: `bool`
- **Description**: Gets whether the operation was successful
- **Access**: Get only

#### Error
- **Type**: `AsyncEndpointError?`
- **Description**: Gets the error information if the operation failed, otherwise null
- **Access**: Get only

### Static Methods

#### Success
```csharp
public static MethodResult Success()
```
**Returns:** A `MethodResult` indicating success

**Description:** Creates a successful result.

```csharp
// Example
var result = MethodResult.Success();
```

#### Failure (Exception)
```csharp
public static MethodResult Failure(Exception exception)
```
**Parameters:**
- `exception` (`Exception`): The exception that occurred

**Returns:** A `MethodResult` indicating failure

**Description:** Creates a failed result with the specified exception.

```csharp
// Example
var result = MethodResult.Failure(new InvalidOperationException("Operation failed"));
```

#### Failure (AsyncEndpointError)
```csharp
public static MethodResult Failure(AsyncEndpointError error)
```
**Parameters:**
- `error` (`AsyncEndpointError`): The error information

**Returns:** A `MethodResult` indicating failure

**Description:** Creates a failed result with the specified error.

```csharp
// Example
var error = AsyncEndpointError.FromCode("OPERATION_ERROR", "Operation failed");
var result = MethodResult.Failure(error);
```