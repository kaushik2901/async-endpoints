---
sidebar_position: 9
---

# Advanced Features

## HTTP Context Access

AsyncEndpoints preserves and provides access to the original HTTP context in your handlers:

```csharp
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<InputRequest> context, CancellationToken token)
{
    // Access request body
    var request = context.Request;
    
    // Access HTTP headers
    var headers = context.Headers;
    var authorization = headers["Authorization"]?.FirstOrDefault();
    var userAgent = headers["User-Agent"]?.FirstOrDefault();
    var customHeader = headers["X-Custom-Header"]?.FirstOrDefault();
    
    // Access route parameters
    var routeParams = context.RouteParams;
    var resourceId = routeParams["resourceId"]?.ToString();
    
    // Access query parameters
    var queryParams = context.QueryParams;
    var format = queryParams.FirstOrDefault(x => x.Key == "format").Value?.FirstOrDefault();
    
    // Process with full context
    var result = await ProcessWithContext(request, headers, routeParams, queryParams, token);
    
    return MethodResult<ProcessResult>.Success(result);
}
```

## Custom Response Factories

Customize how responses are formatted for different scenarios:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    // Customize job submission response
    options.ResponseConfigurations.JobSubmittedResponseFactory = async (job, context) =>
    {
        context.Response.Headers.Append("Async-Job-Id", job.Id.ToString());
        context.Response.Headers.Append("Location", $"/jobs/{job.Id}");
        
        return Results.Json(new
        {
            jobId = job.Id,
            statusUrl = $"/jobs/{job.Id}",
            status = job.Status,
            submittedAt = job.CreatedAt
        }, statusCode: 202);
    };

    // Customize job status response
    options.ResponseConfigurations.JobStatusResponseFactory = async (jobResult, context) =>
    {
        if (jobResult.IsSuccess && jobResult.Data != null)
        {
            var job = jobResult.Data;
            
            return Results.Json(new
            {
                id = job.Id,
                name = job.Name,
                status = job.Status,
                progress = CalculateProgress(job.Status),
                result = job.Status == JobStatus.Completed ? job.Result : null,
                error = job.Status == JobStatus.Failed ? job.Error?.Message : null,
                retryCount = job.RetryCount,
                maxRetries = job.MaxRetries,
                timestamps = new
                {
                    createdAt = job.CreatedAt,
                    startedAt = job.StartedAt,
                    completedAt = job.CompletedAt,
                    lastUpdated = job.LastUpdatedAt
                }
            });
        }
        
        return Results.NotFound(new { error = "Job not found" });
    };

    // Customize exception responses
    options.ResponseConfigurations.ExceptionResponseFactory = async (exception, context) =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(exception, "Unhandled exception in async endpoint");
        
        return Results.Problem(new ProblemDetails
        {
            Title = "Processing Error",
            Detail = "An error occurred while processing your request",
            Status = 500,
            Instance = context.Request.Path
        });
    };
});

private static int CalculateProgress(JobStatus status)
{
    return status switch
    {
        JobStatus.Queued => 20,
        JobStatus.InProgress => 60,
        JobStatus.Completed => 100,
        JobStatus.Failed => -1,
        JobStatus.Canceled => -1,
        _ => 0
    };
}
```

## Validation Middleware

Implement custom validation that runs before the job is queued:

```csharp
app.MapAsyncPost<DataRequest>("ProcessData", "/api/process-data",
    async (HttpContext context, DataRequest request, CancellationToken token) =>
    {
        // Access route values
        var resourceId = context.Request.RouteValues["resourceId"]?.ToString();
        
        // Access headers
        var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
        
        // Perform validation
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Results.Unauthorized();
        }
        
        if (request.Data.Length > 10000)
        {
            return Results.BadRequest("Data exceeds maximum length of 10000 characters");
        }
        
        if (request.Priority < 1 || request.Priority > 5)
        {
            return Results.BadRequest("Priority must be between 1 and 5");
        }
        
        // Return null to continue queuing the job
        return null;
    });
```

## Distributed Recovery Configuration

Fine-tune distributed job recovery for multi-instance deployments:

```csharp
builder.Services.AddAsyncEndpointsWorker(recoveryConfiguration =>
{
    recoveryConfiguration.EnableDistributedJobRecovery = true;
    recoveryConfiguration.JobTimeoutMinutes = 45;                 // Jobs stuck longer than this are recovered
    recoveryConfiguration.RecoveryCheckIntervalSeconds = 180;     // Check every 3 minutes
    recoveryConfiguration.MaximumRetries = 2;                     // Additional retries for recovered jobs
});
```

## Serialization Context

Configure JSON serialization for your types:

```csharp
// ApplicationJsonSerializationContext.cs
using System.Text.Json.Serialization;

[JsonSerializable(typeof(DataRequest))]
[JsonSerializable(typeof(DataResponse))]
[JsonSerializable(typeof(Job))]
public partial class ApplicationJsonSerializationContext : JsonSerializerContext
{
}

// In Program.cs
builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsInMemoryStore()
    .AddAsyncEndpointsJsonTypeInfoResolver(ApplicationJsonSerializationContext.Default);
```

## Worker Concurrency Control

Configure worker concurrency for performance optimization:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount * 2;
    options.WorkerConfigurations.BatchSize = 10;              // Process jobs in batches
    options.WorkerConfigurations.PollingIntervalMs = 500;     // Check for jobs every 500ms
    options.WorkerConfigurations.MaximumQueueSize = 100;      // Max jobs in queue
});
```

## Custom Job Storage

Implement custom job storage by extending the `IJobStore` interface:

```csharp
public class CustomJobStore : IJobStore
{
    public bool SupportsJobRecovery => false; // Your implementation may or may not support recovery

    public async Task<MethodResult> CreateJob(Job job, CancellationToken cancellationToken)
    {
        // Custom implementation
        return MethodResult.Success();
    }

    public async Task<MethodResult<Job>> GetJobById(Guid id, CancellationToken cancellationToken)
    {
        // Custom implementation
        return MethodResult<Job>.Success(job);
    }

    public async Task<MethodResult> UpdateJob(Job job, CancellationToken cancellationToken)
    {
        // Custom implementation
        return MethodResult.Success();
    }

    public async Task<MethodResult<Job>> ClaimNextJobForWorker(Guid workerId, CancellationToken cancellationToken)
    {
        // Custom implementation
        return MethodResult<Job>.Success(job);
    }
}

// Register the custom store
services.AddSingleton<IJobStore, CustomJobStore>();
```

## Background Service Configuration

Configure the background worker service:

```csharp
builder.Services.AddAsyncEndpointsWorker(workerConfig =>
{
    // Worker configuration is passed to the background service
});
```

## Job Status Filtering

Implement custom logic to filter or transform job status responses:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.ResponseConfigurations.JobStatusResponseFactory = async (jobResult, context) =>
    {
        if (jobResult.IsSuccess && jobResult.Data != null)
        {
            var job = jobResult.Data;
            
            // Apply security filtering - don't expose internal data to clients
            var userId = context.Request.Headers["X-User-Id"].FirstOrDefault();
            var isJobOwner = await IsJobOwnerAsync(job.Id, userId);
            
            if (!isJobOwner)
            {
                // Return minimal status for unauthorized access
                return Results.Json(new
                {
                    id = job.Id,
                    status = job.Status,
                    lastUpdated = job.LastUpdatedAt
                });
            }
            
            // Return full details for authorized access
            return Results.Json(job);
        }
        
        return Results.NotFound();
    };
});
```

## Job Prioritization

Implement job prioritization by customizing the Redis storage or creating custom logic:

```csharp
// In your request handler, you can influence job priority by adjusting the score in Redis
// or by implementing custom queuing logic in your custom store
```

## Circuit Breaker Pattern

Implement circuit breaker logic to prevent system overload:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.WorkerConfigurations.MaximumQueueSize = 100; // Prevent queue overflow
});

// Additionally, you can implement custom validation to check system capacity
app.MapAsyncPost<DataRequest>("ProcessData", "/api/process-data",
    async (HttpContext context, DataRequest request, CancellationToken token) =>
    {
        // Check system load before accepting job
        var queueSize = await GetQueueSizeAsync(); // Custom method to get queue size
        if (queueSize > 90) // 90% of maximum
        {
            return Results.Problem("System is overloaded, please try again later", statusCode: 503);
        }
        
        return null; // Continue queuing the job
    });
```

## Monitoring and Health Checks

Add health checks for your async processing:

```csharp
// Add to Program.cs
builder.Services.AddHealthChecks()
    .AddCheck<AsyncEndpointsHealthCheck>("async-endpoints");

var app = builder.Build();

app.MapHealthChecks("/health");
```

## Performance Optimization

### Batch Processing
Adjust batch sizes based on your workload:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.JobManagerConfiguration.MaxClaimBatchSize = 20;  // Claim up to 20 jobs at once
    options.WorkerConfigurations.BatchSize = 15;             // Process up to 15 jobs per batch
});
```

### Connection Pooling
For Redis implementations, connection pooling is handled automatically by StackExchange.Redis.

## Error Classification

Customize error classification for better monitoring:

```csharp
// Implement custom error classification in your handlers
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<InputRequest> context, CancellationToken token)
{
    try
    {
        var result = await ProcessRequest(context.Request, token);
        return MethodResult<ProcessResult>.Success(result);
    }
    catch (ValidationException ex)
    {
        // Return validation errors differently from processing errors
        return MethodResult<ProcessResult>.Failure(AsyncEndpointError.FromCode("VALIDATION_ERROR", ex.Message));
    }
    catch (ExternalServiceException ex)
    {
        // Mark as retryable error
        return MethodResult<ProcessResult>.Failure(AsyncEndpointError.FromCode("EXTERNAL_SERVICE_ERROR", ex.Message, ex));
    }
    catch (Exception ex)
    {
        // General error
        return MethodResult<ProcessResult>.Failure(ex);
    }
}
```

## Testing Considerations

When testing async endpoints, consider:

1. **Mocking**: Mock external dependencies in your handlers
2. **State Verification**: Verify job state transitions in your storage
3. **Integration Testing**: Test the full flow from endpoint to handler to storage
4. **Error Scenarios**: Test retry behavior and error handling
5. **Concurrency**: Test multiple concurrent job processing scenarios