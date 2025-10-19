---
sidebar_position: 3
title: Core Interfaces
---

# Core Interfaces

This page provides detailed reference documentation for all AsyncEndpoints core interfaces, including their methods, properties, and usage examples.

## IAsyncEndpointRequestHandler\&lt;TRequest, TResponse&gt;

### Interface Definition
```csharp
public interface IAsyncEndpointRequestHandler<TRequest, TResponse>
```

### Type Parameters
- **TRequest**: The type of the request object
- **TResponse**: The type of the response object

### Methods

#### HandleAsync
```csharp
Task<MethodResult<TResponse>> HandleAsync(AsyncContext<TRequest> context, CancellationToken token)
```

**Parameters:**
- `context` (`AsyncContext\<TRequest>`): The context containing the request object and associated HTTP context information
- `token` (`CancellationToken`): A cancellation token to cancel the operation

**Returns:**
- `Task<MethodResult<TResponse>>`: A `MethodResult\<TResponse>` containing the result of the operation

**Description:**
Handles the asynchronous request and returns a result.

### Example
```csharp
public class ProcessDataHandler : IAsyncEndpointRequestHandler<DataRequest, ProcessResult>
{
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        var request = context.Request;
        var headers = context.Headers;
        var routeParams = context.RouteParams;
        var queryParams = context.QueryParams;
        
        // Process the request
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

## IAsyncEndpointRequestHandler\&lt;TResponse&gt;

### Interface Definition
```csharp
public interface IAsyncEndpointRequestHandler<TResponse>
```

### Type Parameters
- **TResponse**: The type of the response object

### Methods

#### HandleAsync
```csharp
Task<MethodResult<TResponse>> HandleAsync(AsyncContext context, CancellationToken token)
```

**Parameters:**
- `context` (`AsyncContext`): The context containing HTTP context information
- `token` (`CancellationToken`): A cancellation token to cancel the operation

**Returns:**
- `Task<MethodResult<TResponse>>`: A `MethodResult\<TResponse>` containing the result of the operation

**Description:**
Handles the asynchronous request without body data and returns a result.

### Example
```csharp
public class GenerateReportHandler : IAsyncEndpointRequestHandler<ReportResult>
{
    public async Task<MethodResult<ReportResult>> HandleAsync(AsyncContext context, CancellationToken token)
    {
        var headers = context.Headers;
        var routeParams = context.RouteParams;
        var queryParams = context.QueryParams;
        
        // Process without request body
        var result = new ReportResult
        {
            ReportData = "Generated report data...",
            GeneratedAt = DateTime.UtcNow
        };
        
        return MethodResult<ReportResult>.Success(result);
    }
}
```

---

## IJobStore

### Interface Definition
```csharp
public interface IJobStore
```

### Properties

#### SupportsJobRecovery
- **Type**: `bool`
- **Description**: Gets whether the job store supports recovery operations

### Methods

#### CreateJob
```csharp
Task<MethodResult> CreateJob(Job job, CancellationToken cancellationToken)
```

**Parameters:**
- `job` (`Job`): The job to create
- `cancellationToken` (`CancellationToken`): A cancellation token

**Returns:**
- `Task<MethodResult>`: A `MethodResult` indicating success or failure

**Description:**
Creates a new job in the store.

---

#### GetJobById
```csharp
Task<MethodResult<Job>> GetJobById(Guid id, CancellationToken cancellationToken)
```

**Parameters:**
- `id` (`Guid`): The ID of the job to retrieve
- `cancellationToken` (`CancellationToken`): A cancellation token

**Returns:**
- `Task<MethodResult<Job>>`: A `MethodResult<Job>` containing the job if found, or an error

**Description:**
Retrieves a job by its ID from the store.

---

#### UpdateJob
```csharp
Task<MethodResult> UpdateJob(Job job, CancellationToken cancellationToken)
```

**Parameters:**
- `job` (`Job`): The job to update
- `cancellationToken` (`CancellationToken`): A cancellation token

**Returns:**
- `Task<MethodResult>`: A `MethodResult` indicating success or failure

**Description:**
Updates an existing job in the store.

---

#### ClaimNextJobForWorker
```csharp
Task<MethodResult<Job>> ClaimNextJobForWorker(Guid workerId, CancellationToken cancellationToken)
```

**Parameters:**
- `workerId` (`Guid`): The ID of the worker claiming the job
- `cancellationToken` (`CancellationToken`): A cancellation token

**Returns:**
- `Task<MethodResult<Job>>`: A `MethodResult<Job>` containing the claimed job if available, or null

**Description:**
Claims the next available job for the specified worker.

### Example Implementation
```csharp
public class ExampleJobStore : IJobStore
{
    public bool SupportsJobRecovery => true;
    
    private readonly ConcurrentDictionary<Guid, Job> _jobs = new();
    
    public async Task<MethodResult> CreateJob(Job job, CancellationToken cancellationToken)
    {
        if (_jobs.ContainsKey(job.Id))
        {
            return MethodResult.Failure(
                AsyncEndpointError.FromCode("JOB_EXISTS", $"Job with ID {job.Id} already exists")
            );
        }
        
        _jobs[job.Id] = job;
        return MethodResult.Success();
    }
    
    public async Task<MethodResult<Job>> GetJobById(Guid id, CancellationToken cancellationToken)
    {
        if (_jobs.TryGetValue(id, out var job))
        {
            return MethodResult<Job>.Success(job);
        }
        
        return MethodResult<Job>.Failure(
            AsyncEndpointError.FromCode("JOB_NOT_FOUND", $"Job with ID {id} not found")
        );
    }
    
    public async Task<MethodResult> UpdateJob(Job job, CancellationToken cancellationToken)
    {
        if (!_jobs.ContainsKey(job.Id))
        {
            return MethodResult.Failure(
                AsyncEndpointError.FromCode("JOB_NOT_FOUND", $"Job with ID {job.Id} not found")
            );
        }
        
        _jobs[job.Id] = job;
        return MethodResult.Success();
    }
    
    public async Task<MethodResult<Job>> ClaimNextJobForWorker(Guid workerId, CancellationToken cancellationToken)
    {
        // Find the next available job (simplified logic)
        var nextJob = _jobs.Values
            .Where(j => j.Status == JobStatus.Queued)
            .OrderBy(j => j.CreatedAt)
            .FirstOrDefault();
            
        if (nextJob != null)
        {
            // Claim the job by updating its status and worker ID
            var claimedJob = nextJob.CreateCopy(
                status: JobStatus.InProgress,
                workerId: workerId,
                startedAt: DateTimeOffset.UtcNow
            );
            
            _jobs[nextJob.Id] = claimedJob;
            return MethodResult<Job>.Success(claimedJob);
        }
        
        return MethodResult<Job>.Success(null);
    }
}
```

---

## IJobManager

### Interface Definition
```csharp
public interface IJobManager
```

### Methods

#### SubmitJob
```csharp
Task<MethodResult<Job>> SubmitJob(string jobName, string payload, HttpContext httpContext, CancellationToken cancellationToken)
```

**Parameters:**
- `jobName` (`string`): The name of the job
- `payload` (`string`): The serialized payload data
- `httpContext` (`HttpContext`): The HTTP context for preserving headers, route params, etc.
- `cancellationToken` (`CancellationToken`): A cancellation token

**Returns:**
- `Task<MethodResult<Job>>`: A `MethodResult<Job>` containing the created job or an error

**Description:**
Submits a new job to be processed.

---

#### ClaimNextAvailableJob
```csharp
Task<MethodResult<Job>> ClaimNextAvailableJob(Guid workerId, CancellationToken cancellationToken)
```

**Parameters:**
- `workerId` (`Guid`): The ID of the worker claiming the job
- `cancellationToken` (`CancellationToken`): A cancellation token

**Returns:**
- `Task<MethodResult<Job>>`: A `MethodResult<Job>` containing the claimed job or an error

**Description:**
Claims the next available job for the specified worker.

---

#### ProcessJobSuccess
```csharp
Task<MethodResult> ProcessJobSuccess(Guid jobId, string result, CancellationToken cancellationToken)
```

**Parameters:**
- `jobId` (`Guid`): The ID of the job that completed successfully
- `result` (`string`): The serialized result of the job
- `cancellationToken` (`CancellationToken`): A cancellation token

**Returns:**
- `Task<MethodResult>`: A `MethodResult` indicating success or failure

**Description:**
Marks a job as successfully completed with the provided result.

---

#### ProcessJobFailure
```csharp
Task<MethodResult> ProcessJobFailure(Guid jobId, AsyncEndpointError error, CancellationToken cancellationToken)
```

**Parameters:**
- `jobId` (`Guid`): The ID of the job that failed
- `error` (`AsyncEndpointError`): The error information
- `cancellationToken` (`CancellationToken`): A cancellation token

**Returns:**
- `Task<MethodResult>`: A `MethodResult` indicating success or failure

**Description:**
Marks a job as failed with the provided error information.

---

#### GetJobById
```csharp
Task<MethodResult<Job>> GetJobById(Guid jobId, CancellationToken cancellationToken)
```

**Parameters:**
- `jobId` (`Guid`): The ID of the job to retrieve
- `cancellationToken` (`CancellationToken`): A cancellation token

**Returns:**
- `Task<MethodResult<Job>>`: A `MethodResult<Job>` containing the job or an error

**Description:**
Retrieves a job by its ID.

### Example Usage
```csharp
public class JobProcessingService
{
    private readonly IJobManager _jobManager;
    
    public JobProcessingService(IJobManager jobManager)
    {
        _jobManager = jobManager;
    }
    
    public async Task<MethodResult<Job>> SubmitDataProcessingJob(DataRequest request, HttpContext context)
    {
        var payload = JsonSerializer.Serialize(request);
        return await _jobManager.SubmitJob("ProcessData", payload, context, CancellationToken.None);
    }
    
    public async Task ProcessJobResult(Guid jobId, ProcessResult result, bool success)
    {
        if (success)
        {
            var serializedResult = JsonSerializer.Serialize(result);
            await _jobManager.ProcessJobSuccess(jobId, serializedResult, CancellationToken.None);
        }
        else
        {
            var error = AsyncEndpointError.FromCode("PROCESSING_ERROR", "Failed to process job");
            await _jobManager.ProcessJobFailure(jobId, error, CancellationToken.None);
        }
    }
}
```

---

## IAsyncEndpointRequestDelegate

### Interface Definition
```csharp
public interface IAsyncEndpointRequestDelegate
```

### Methods

#### HandleAsync
```csharp
Task<IResult> HandleAsync<TRequest>(
    string jobName,
    HttpContext httpContext,
    TRequest request,
    Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null,
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `jobName` (`string`): The name of the job to handle
- `httpContext` (`HttpContext`): The HTTP context
- `request` (`TRequest`): The request object
- `handler` (`Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>`): Optional custom handler function
- `cancellationToken` (`CancellationToken`): A cancellation token

**Returns:**
- `Task<IResult>`: An `IResult` representing the HTTP response

**Description:**
Handles the asynchronous request and returns an HTTP result.

### Example
```csharp
public class CustomRequestDelegate
{
    private readonly IAsyncEndpointRequestDelegate _requestDelegate;
    
    public CustomRequestDelegate(IAsyncEndpointRequestDelegate requestDelegate)
    {
        _requestDelegate = requestDelegate;
    }
    
    public async Task<IResult> HandleCustomRequest(HttpContext context, DataRequest request)
    {
        // Custom validation before job submission
        if (string.IsNullOrEmpty(request.Data))
        {
            return Results.BadRequest("Data field is required");
        }
        
        // Submit job with custom handler
        return await _requestDelegate.HandleAsync(
            "ProcessData",
            context,
            request,
            async (ctx, req, token) =>
            {
                // Additional custom validation
                if (req.Data.Length > 1000)
                {
                    return Results.BadRequest("Data too large");
                }
                
                return null; // Continue with job submission
            },
            CancellationToken.None
        );
    }
}
```