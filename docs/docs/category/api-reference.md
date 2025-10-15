---
sidebar_position: 11
---

# API Reference

## Namespaces

### `AsyncEndpoints.Extensions`

Contains extension methods for configuring and mapping AsyncEndpoints functionality.

### `AsyncEndpoints.Handlers`

Contains interfaces and classes for defining request handlers.

### `AsyncEndpoints.JobProcessing`

Contains core job processing classes and interfaces.

### `AsyncEndpoints.Utilities`

Contains utility classes and result types.

### `AsyncEndpoints.Configuration`

Contains configuration classes for AsyncEndpoints.

## Extension Methods

### ServiceCollectionExtensions

#### `AddAsyncEndpoints`

Configure the core AsyncEndpoints services.

```csharp
public static IServiceCollection AddAsyncEndpoints(
    this IServiceCollection services, 
    Action<AsyncEndpointsConfigurations>? configureOptions = null)
```

**Parameters:**
- `services`: The `IServiceCollection` to add services to
- `configureOptions`: Optional action to configure AsyncEndpoints options

**Returns:** The `IServiceCollection` for method chaining.

#### `AddAsyncEndpointsInMemoryStore`

Add an in-memory job store implementation.

```csharp
public static IServiceCollection AddAsyncEndpointsInMemoryStore(
    this IServiceCollection services)
```

**Parameters:**
- `services`: The `IServiceCollection` to add services to

**Returns:** The `IServiceCollection` for method chaining.

#### `AddAsyncEndpointsJsonTypeInfoResolver`

Add a JSON type information resolver.

```csharp
public static IServiceCollection AddAsyncEndpointsJsonTypeInfoResolver(
    this IServiceCollection services, 
    IJsonTypeInfoResolver jsonTypeInfoResolver)
```

**Parameters:**
- `services`: The `IServiceCollection` to add services to
- `jsonTypeInfoResolver`: The JSON type information resolver to use

**Returns:** The `IServiceCollection` for method chaining.

#### `AddAsyncEndpointsWorker`

Add background worker services required to process async jobs.

```csharp
public static IServiceCollection AddAsyncEndpointsWorker(
    this IServiceCollection services,
    Action<AsyncEndpointsRecoveryConfiguration>? recoveryConfiguration = null)
```

**Parameters:**
- `services`: The `IServiceCollection` to add services to
- `recoveryConfiguration`: Optional configuration for distributed job recovery

**Returns:** The `IServiceCollection` for method chaining.

#### `AddAsyncEndpointHandler<TAsyncEndpointRequestHandler, TRequest, TResponse>`

Register an asynchronous endpoint handler for processing requests of type TRequest and returning responses of type TResponse.

```csharp
public static IServiceCollection AddAsyncEndpointHandler<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] 
    TAsyncEndpointRequestHandler, TRequest, TResponse>(
    this IServiceCollection services, 
    string jobName)
    where TAsyncEndpointRequestHandler : class, IAsyncEndpointRequestHandler<TRequest, TResponse>
```

**Type Parameters:**
- `TAsyncEndpointRequestHandler`: The type of the handler that implements `IAsyncEndpointRequestHandler<TRequest, TResponse>`
- `TRequest`: The type of the request object
- `TResponse`: The type of the response object

**Parameters:**
- `services`: The `IServiceCollection` to add services to
- `jobName`: The unique name of the job, used to identify the specific handler

**Returns:** The `IServiceCollection` for method chaining.

#### `AddAsyncEndpointHandler<TAsyncEndpointRequestHandler, TResponse>`

Add an asynchronous endpoint handler for requests without body.

```csharp
public static IServiceCollection AddAsyncEndpointHandler<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] 
    TAsyncEndpointRequestHandler, TResponse>(
    this IServiceCollection services,
    string jobName)
    where TAsyncEndpointRequestHandler : class, IAsyncEndpointRequestHandler<TResponse>
```

**Type Parameters:**
- `TAsyncEndpointRequestHandler`: The type of the handler that implements `IAsyncEndpointRequestHandler<TResponse>`
- `TResponse`: The type of the response object

**Parameters:**
- `services`: The `IServiceCollection` to add services to
- `jobName`: A unique name for the async job, used for identifying the handler

**Returns:** The `IServiceCollection` for method chaining.

### RouteBuilderExtensions

#### `MapAsyncPost<TRequest>`

Maps an asynchronous POST endpoint that processes requests in the background.

```csharp
public static IEndpointConventionBuilder MapAsyncPost<TRequest>(
    this IEndpointRouteBuilder endpoints,
    string jobName,
    string pattern,
    Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null)
```

**Type Parameters:**
- `TRequest`: The type of the request object

**Parameters:**
- `endpoints`: The `IEndpointRouteBuilder` to add the route to
- `jobName`: A unique name for the async job, used for identifying the handler
- `pattern`: The URL pattern for the endpoint
- `handler`: Optional custom handler function to process the request

**Returns:** An `IEndpointConventionBuilder` that can be used to further configure the endpoint.

#### `MapAsyncPost`

Maps an asynchronous POST endpoint that processes requests without body in the background.

```csharp
public static IEndpointConventionBuilder MapAsyncPost(
    this IEndpointRouteBuilder endpoints,
    string jobName,
    string pattern,
    Func<HttpContext, NoBodyRequest, CancellationToken, Task<IResult?>?>? handler = null)
```

**Parameters:**
- `endpoints`: The `IEndpointRouteBuilder` to add the route to
- `jobName`: A unique name for the async job, used for identifying the handler
- `pattern`: The URL pattern for the endpoint
- `handler`: Optional custom handler function to process the request

**Returns:** An `IEndpointConventionBuilder` that can be used to further configure the endpoint.

#### `MapAsyncGetJobDetails`

Maps an asynchronous GET endpoint that fetches job responses by job ID.

```csharp
public static IEndpointConventionBuilder MapAsyncGetJobDetails(
    this IEndpointRouteBuilder endpoints,
    string pattern = "/jobs/{jobId:guid}")
```

**Parameters:**
- `endpoints`: The `IEndpointRouteBuilder` to add the route to
- `pattern`: The URL pattern for the endpoint. Should contain a {jobId} parameter

**Returns:** An `IEndpointConventionBuilder` that can be used to further configure the endpoint.

## Handler Interfaces

### `IAsyncEndpointRequestHandler<TRequest, TResponse>`

Defines a contract for handling asynchronous endpoint requests of type TRequest and returning responses of type TResponse.

```csharp
public interface IAsyncEndpointRequestHandler<TRequest, TResponse>
{
    Task<MethodResult<TResponse>> HandleAsync(
        AsyncContext<TRequest> context, 
        CancellationToken token);
}
```

**Methods:**
- `HandleAsync(AsyncContext<TRequest> context, CancellationToken token)`: Handles the asynchronous request and returns a result.

**Parameters:**
- `context`: The context containing the request object and associated HTTP context information
- `token`: A cancellation token to cancel the operation

**Returns:** A `MethodResult<TResponse>` containing the result of the operation.

### `IAsyncEndpointRequestHandler<TResponse>`

Defines a contract for handling asynchronous endpoint requests without body data, returning responses of type TResponse.

```csharp
public interface IAsyncEndpointRequestHandler<TResponse>
{
    Task<MethodResult<TResponse>> HandleAsync(
        AsyncContext context, 
        CancellationToken token);
}
```

**Methods:**
- `HandleAsync(AsyncContext context, CancellationToken token)`: Handles the asynchronous request without body data and returns a result.

**Parameters:**
- `context`: The context containing HTTP context information
- `token`: A cancellation token to cancel the operation

**Returns:** A `MethodResult<TResponse>` containing the result of the operation.

## Job Classes

### `Job`

Represents an asynchronous job in the AsyncEndpoints system.

```csharp
public sealed class Job
{
    public Guid Id { get; init; }
    public string Name { get; set; }
    public JobStatus Status { get; set; }
    public Dictionary<string, List<string?>> Headers { get; set; }
    public Dictionary<string, object?> RouteParams { get; set; }
    public List<KeyValuePair<string, List<string?>>> QueryParams { get; set; }
    public string Payload { get; init; }
    public string? Result { get; set; }
    public AsyncEndpointError? Error { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; }
    public DateTime? RetryDelayUntil { get; set; }
    public Guid? WorkerId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; }
    public bool IsCanceled => Status == JobStatus.Canceled;

    public static Job Create(Guid id, string name, string payload, IDateTimeProvider dateTimeProvider);
    public static Job Create(
        Guid id, 
        string name, 
        string payload,
        Dictionary<string, List<string?>> headers,
        Dictionary<string, object?> routeParams,
        List<KeyValuePair<string, List<string?>>> queryParams,
        IDateTimeProvider dateTimeProvider);
    
    public void UpdateStatus(JobStatus status, IDateTimeProvider dateTimeProvider);
    public void SetResult(string result, IDateTimeProvider dateTimeProvider);
    public void SetError(AsyncEndpointError error, IDateTimeProvider dateTimeProvider);
    public void SetError(string error, IDateTimeProvider dateTimeProvider);
    public void IncrementRetryCount();
    public void SetRetryTime(DateTime delayUntil);
    public Job CreateCopy(/* parameters */);
}
```

### `JobStatus`

Represents the status of an asynchronous job in the system.

```csharp
public enum JobStatus
{
    Queued = 100,      // Job has been created and is waiting to be processed
    Scheduled = 200,   // Job has been scheduled for delayed execution
    InProgress = 300,  // Job is currently being processed by a worker
    Completed = 400,   // Job has completed successfully
    Failed = 500,      // Job has failed and will not be retried
    Canceled = 600     // Job has been canceled and will not be processed
}
```

## Utility Classes

### `MethodResult<T>`

Represents the result of a method operation that returns a value of type T.

```csharp
public class MethodResult<T> : MethodResult
{
    public T Data { get; }
    public T? DataOrNull { get; }

    public static MethodResult<T> Success(T? data);
    public static MethodResult<T> Failure(AsyncEndpointError error);
    public static MethodResult<T> Failure(string errorMessage);
    public static MethodResult<T> Failure(Exception exception);
}
```

### `MethodResult`

Represents the result of a method operation, indicating whether it was successful or failed.

```csharp
public class MethodResult
{
    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public AsyncEndpointError Error { get; }

    public static MethodResult Success();
    public static MethodResult Failure(AsyncEndpointError error);
    public static MethodResult Failure(string errorMessage);
    public static MethodResult Failure(Exception exception);
}
```

### `AsyncEndpointError`

Represents an error that occurred during async endpoint processing.

```csharp
public sealed class AsyncEndpointError
{
    public string Code { get; }
    public string Message { get; }
    public Exception? Exception { get; }

    public static AsyncEndpointError FromCode(string code, string message, Exception? exception = null);
    public static AsyncEndpointError FromMessage(string message, Exception? exception = null);
    public static AsyncEndpointError FromException(Exception exception);
}
```

## Context Classes

### `AsyncContext<TRequest>`

Represents the context for an asynchronous request, containing the request object and associated HTTP context information.

```csharp
public sealed class AsyncContext<TRequest> : AsyncContext
{
    public TRequest Request { get; init; }
}
```

### `AsyncContext`

Represents the base context for an asynchronous request, containing associated HTTP context information.

```csharp
public class AsyncContext
{
    public IDictionary<string, List<string?>> Headers { get; init; }
    public IDictionary<string, object?> RouteParams { get; set; }
    public IEnumerable<KeyValuePair<string, List<string?>>> QueryParams { get; init; }
}
```

## Configuration Classes

### `AsyncEndpointsConfigurations`

Configuration settings for the AsyncEndpoints library.

```csharp
public sealed class AsyncEndpointsConfigurations
{
    public AsyncEndpointsWorkerConfigurations WorkerConfigurations { get; set; }
    public AsyncEndpointsJobManagerConfiguration JobManagerConfiguration { get; set; }
    public AsyncEndpointsResponseConfigurations ResponseConfigurations { get; set; }
}
```

### `AsyncEndpointsWorkerConfigurations`

Configuration settings for AsyncEndpoints background workers.

```csharp
public sealed class AsyncEndpointsWorkerConfigurations
{
    public Guid WorkerId { get; set; }
    public int MaximumConcurrency { get; set; }
    public int PollingIntervalMs { get; set; }
    public int JobTimeoutMinutes { get; set; }
    public int BatchSize { get; set; }
    public int MaximumQueueSize { get; set; }
}
```

### `AsyncEndpointsJobManagerConfiguration`

Configuration settings for the AsyncEndpoints job manager.

```csharp
public sealed class AsyncEndpointsJobManagerConfiguration
{
    public int DefaultMaxRetries { get; set; }
    public double RetryDelayBaseSeconds { get; set; }
    public TimeSpan JobClaimTimeout { get; set; }
    public int MaxConcurrentJobs { get; set; }
    public int JobPollingIntervalMs { get; set; }
    public int MaxClaimBatchSize { get; set; }
    public TimeSpan StaleJobClaimCheckInterval { get; set; }
}
```

### `AsyncEndpointsResponseConfigurations`

Configuration settings for AsyncEndpoints response behavior.

```csharp
public sealed class AsyncEndpointsResponseConfigurations
{
    public Func<Job, HttpContext, Task<IResult>> JobSubmittedResponseFactory { get; set; }
    public Func<MethodResult<Job>, HttpContext, Task<IResult>> JobStatusResponseFactory { get; set; }
    public Func<Exception, HttpContext, Task<IResult>> ExceptionResponseFactory { get; set; }
}
```

### `AsyncEndpointsRecoveryConfiguration`

Configuration settings for distributed job recovery.

```csharp
public sealed class AsyncEndpointsRecoveryConfiguration
{
    public bool EnableDistributedJobRecovery { get; set; }
    public int JobTimeoutMinutes { get; set; }
    public int RecoveryCheckIntervalSeconds { get; set; }
    public int MaximumRetries { get; set; }
}
```

## Constants

### `AsyncEndpointsConstants`

Contains constant values for the AsyncEndpoints library.

```csharp
public static class AsyncEndpointsConstants
{
    public const string AsyncEndpointTag = "AsyncEndpoint";
    public const string JobIdHeaderName = "Async-Job-Id";
    public const int MaximumRetries = 3;
    public const int DefaultPollingIntervalMs = 1000;
    public const int DefaultJobTimeoutMinutes = 30;
    public const int DefaultBatchSize = 5;
    public const int DefaultMaximumQueueSize = 50;
    // ... other constants
}
```

## Core Interfaces

### `IJobStore`

Interface for job storage implementations.

```csharp
public interface IJobStore
{
    bool SupportsJobRecovery { get; }
    Task<MethodResult> CreateJob(Job job, CancellationToken cancellationToken);
    Task<MethodResult<Job>> GetJobById(Guid id, CancellationToken cancellationToken);
    Task<MethodResult> UpdateJob(Job job, CancellationToken cancellationToken);
    Task<MethodResult<Job>> ClaimNextJobForWorker(Guid workerId, CancellationToken cancellationToken);
}
```

### `IJobManager`

Interface for job management operations.

```csharp
public interface IJobManager
{
    Task<MethodResult<Job>> SubmitJob(string jobName, string payload, HttpContext httpContext, CancellationToken cancellationToken);
    Task<MethodResult<Job>> ClaimNextAvailableJob(Guid workerId, CancellationToken cancellationToken);
    Task<MethodResult> ProcessJobSuccess(Guid jobId, string result, CancellationToken cancellationToken);
    Task<MethodResult> ProcessJobFailure(Guid jobId, AsyncEndpointError error, CancellationToken cancellationToken);
    Task<MethodResult<Job>> GetJobById(Guid jobId, CancellationToken cancellationToken);
}
```

## Supporting Types

### `NoBodyRequest`

Represents a request without a body payload.

```csharp
public sealed class NoBodyRequest
{
    public static NoBodyRequest CreateInstance();
}
```

This API reference provides comprehensive information about all public types, members, and functionality available in AsyncEndpoints. For additional details about specific methods or properties, refer to the XML documentation comments in the source code.