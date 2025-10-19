---
sidebar_position: 1
title: Advanced Features
---

# Advanced Features

This page covers the advanced features of AsyncEndpoints that provide enhanced functionality beyond the basic async processing capabilities.

## Custom Validation Middleware

You can add validation logic before jobs are queued using custom middleware functions:

```csharp
app.MapAsyncPost<DataRequest>("ProcessData", "/api/process-data", 
    async (HttpContext context, DataRequest request, CancellationToken token) => 
    {
        // Custom validation logic
        if (string.IsNullOrWhiteSpace(request.Data))
        {
            return Results.BadRequest(new { error = "Data field is required" });
        }
        
        if (request.ProcessingPriority < 1 || request.ProcessingPriority > 5)
        {
            return Results.BadRequest(new { error = "Priority must be between 1 and 5" });
        }
        
        // Check for rate limiting
        var userId = context.Request.Headers["X-User-Id"].FirstOrDefault();
        if (await IsRateLimited(userId))
        {
            return Results.TooManyRequests("Rate limit exceeded");
        }
        
        // Return null to continue with job queuing
        return null;
    });

private async Task<bool> IsRateLimited(string userId)
{
    // Implement your rate limiting logic
    return false; // Placeholder
}
```

### Complex Validation Example

```csharp
app.MapAsyncPost<ComplexRequest>("ComplexOperation", "/api/complex", 
    async (HttpContext context, ComplexRequest request, CancellationToken token) => 
    {
        // Multiple validation checks
        var validationErrors = new List<string>();
        
        if (request.Data == null || request.Data.Count == 0)
        {
            validationErrors.Add("Data collection cannot be empty");
        }
        
        if (request.TimeoutSeconds < 1 || request.TimeoutSeconds > 3600)
        {
            validationErrors.Add("Timeout must be between 1 and 3600 seconds");
        }
        
        // Validate data items
        for (int i = 0; i < request.Data?.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(request.Data[i]))
            {
                validationErrors.Add($"Data item at index {i} cannot be empty");
            }
        }
        
        if (validationErrors.Any())
        {
            return Results.UnprocessableEntity(new { errors = validationErrors });
        }
        
        // Additional business logic validation
        if (!await ValidateBusinessRules(request))
        {
            return Results.BadRequest(new { error = "Business rules validation failed" });
        }
        
        return null; // Continue queuing
    });

private async Task<bool> ValidateBusinessRules(ComplexRequest request)
{
    // Implement business rule validation
    return true; // Placeholder
}
```

## HTTP Context Preservation

AsyncEndpoints preserves the full HTTP context through the job lifecycle, making it available in your handlers:

### Accessing Headers

```csharp
public class ProcessDataHandler(ILogger<ProcessDataHandler> logger) 
    : IAsyncEndpointRequestHandler<DataRequest, ProcessResult>
{
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        var headers = context.Headers;
        
        // Access specific headers
        var authorization = headers.GetValueOrDefault("Authorization", new List<string?>());
        var contentType = headers.GetValueOrDefault("Content-Type", new List<string?>());
        var userAgent = headers.GetValueOrDefault("User-Agent", new List<string?>());
        var userId = headers.GetValueOrDefault("X-User-Id", new List<string?>());
        
        // Use headers for business logic
        if (authorization != null && authorization.Any())
        {
            logger.LogInformation("Processing request for user with auth token");
        }
        
        // Continue with processing
        return MethodResult<ProcessResult>.Success(new ProcessResult());
    }
}
```

### Accessing Route Parameters

```csharp
// Route: /api/users/{userId}/process
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
{
    var routeParams = context.RouteParams;
    
    // Access route parameters
    var userId = routeParams.GetValueOrDefault("userId", null)?.ToString();
    
    if (userId != null)
    {
        logger.LogInformation("Processing request for user ID: {UserId}", userId);
        
        // Use user ID in processing logic
        var userData = await GetUserById(userId);
        
        // Continue processing with user context
    }
    
    return MethodResult<ProcessResult>.Success(new ProcessResult());
}
```

### Accessing Query Parameters

```csharp
// Request: /api/process?format=json&priority=high&tags=important,urgent
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
{
    var queryParams = context.QueryParams;
    
    // Find specific query parameters
    var formatParam = queryParams.FirstOrDefault(q => q.Key == "format");
    var format = formatParam.Value?.FirstOrDefault();
    
    var priorityParam = queryParams.FirstOrDefault(q => q.Key == "priority");
    var priority = priorityParam.Value?.FirstOrDefault() ?? "normal";
    
    var tagsParam = queryParams.FirstOrDefault(q => q.Key == "tags");
    var tags = tagsParam.Value?.FirstOrDefault()?.Split(',') ?? new string[0];
    
    logger.LogInformation("Processing with format: {Format}, priority: {Priority}, tags: {Tags}", 
        format, priority, string.Join(",", tags));
    
    // Use query parameters in processing logic
    return MethodResult<ProcessResult>.Success(new ProcessResult());
}
```

## Custom Job Stores

While AsyncEndpoints provides in-memory and Redis storage, you can implement custom storage by implementing the `IJobStore` interface:

```csharp
public class CustomDatabaseJobStore : IJobStore
{
    public bool SupportsJobRecovery => true; // Implement if your store supports recovery
    
    private readonly IDbConnection _connection;
    private readonly ILogger<CustomDatabaseJobStore> _logger;
    
    public CustomDatabaseJobStore(IDbConnection connection, ILogger<CustomDatabaseJobStore> logger)
    {
        _connection = connection;
        _logger = logger;
    }
    
    public async Task<MethodResult> CreateJob(Job job, CancellationToken cancellationToken)
    {
        try
        {
            var sql = @"
                INSERT INTO Jobs (Id, Name, Status, Headers, RouteParams, QueryParams, Payload, 
                                RetryCount, MaxRetries, RetryDelayUntil, WorkerId, CreatedAt, 
                                StartedAt, CompletedAt, LastUpdatedAt, Result, Error)
                VALUES (@Id, @Name, @Status, @Headers, @RouteParams, @QueryParams, @Payload,
                        @RetryCount, @MaxRetries, @RetryDelayUntil, @WorkerId, @CreatedAt,
                        @StartedAt, @CompletedAt, @LastUpdatedAt, @Result, @Error)";
            
            await _connection.ExecuteAsync(sql, new
            {
                job.Id,
                job.Name,
                Status = job.Status.ToString(),
                Headers = Serialize(job.Headers),
                RouteParams = Serialize(job.RouteParams),
                QueryParams = Serialize(job.QueryParams),
                job.Payload,
                job.RetryCount,
                job.MaxRetries,
                RetryDelayUntil = job.RetryDelayUntil,
                job.WorkerId,
                job.CreatedAt,
                job.StartedAt,
                job.CompletedAt,
                job.LastUpdatedAt,
                job.Result,
                Error = job.Error != null ? Serialize(job.Error) : null
            });
            
            return MethodResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating job {JobId}", job.Id);
            return MethodResult.Failure(AsyncEndpointError.FromCode("DB_ERROR", ex.Message, ex));
        }
    }
    
    public async Task<MethodResult<Job>> GetJobById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var sql = "SELECT * FROM Jobs WHERE Id = @Id";
            var record = await _connection.QueryFirstOrDefaultAsync(sql, new { Id = id });
            
            if (record == null)
            {
                return MethodResult<Job>.Failure(AsyncEndpointError.FromCode("JOB_NOT_FOUND", "Job not found"));
            }
            
            var job = new Job
            {
                Id = record.Id,
                Name = record.Name,
                Status = Enum.Parse<JobStatus>(record.Status),
                Headers = Deserialize<Dictionary<string, List<string?>>>(record.Headers) ?? new Dictionary<string, List<string?>>(),
                RouteParams = Deserialize<Dictionary<string, object?>>(record.RouteParams) ?? new Dictionary<string, object?>(),
                QueryParams = Deserialize<List<KeyValuePair<string, List<string?>>>>(record.QueryParams) ?? new List<KeyValuePair<string, List<string?>>>(),
                Payload = record.Payload,
                RetryCount = record.RetryCount,
                MaxRetries = record.MaxRetries,
                RetryDelayUntil = record.RetryDelayUntil,
                WorkerId = record.WorkerId,
                CreatedAt = record.CreatedAt,
                StartedAt = record.StartedAt,
                CompletedAt = record.CompletedAt,
                LastUpdatedAt = record.LastUpdatedAt,
                Result = record.Result,
                Error = Deserialize<AsyncEndpointError>(record.Error)
            };
            
            return MethodResult<Job>.Success(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job {JobId}", id);
            return MethodResult<Job>.Failure(AsyncEndpointError.FromCode("DB_ERROR", ex.Message, ex));
        }
    }
    
    public async Task<MethodResult> UpdateJob(Job job, CancellationToken cancellationToken)
    {
        try
        {
            var sql = @"
                UPDATE Jobs 
                SET Status = @Status, Headers = @Headers, RouteParams = @RouteParams, 
                    QueryParams = @QueryParams, Payload = @Payload, RetryCount = @RetryCount, 
                    MaxRetries = @MaxRetries, RetryDelayUntil = @RetryDelayUntil, 
                    WorkerId = @WorkerId, StartedAt = @StartedAt, CompletedAt = @CompletedAt, 
                    LastUpdatedAt = @LastUpdatedAt, Result = @Result, Error = @Error
                WHERE Id = @Id";
            
            var rowsAffected = await _connection.ExecuteAsync(sql, new
            {
                job.Id,
                Status = job.Status.ToString(),
                Headers = Serialize(job.Headers),
                RouteParams = Serialize(job.RouteParams),
                QueryParams = Serialize(job.QueryParams),
                job.Payload,
                job.RetryCount,
                job.MaxRetries,
                RetryDelayUntil = job.RetryDelayUntil,
                job.WorkerId,
                job.StartedAt,
                job.CompletedAt,
                job.LastUpdatedAt,
                job.Result,
                Error = job.Error != null ? Serialize(job.Error) : null
            });
            
            if (rowsAffected == 0)
            {
                return MethodResult.Failure(AsyncEndpointError.FromCode("JOB_NOT_FOUND", "Job not found"));
            }
            
            return MethodResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating job {JobId}", job.Id);
            return MethodResult.Failure(AsyncEndpointError.FromCode("DB_ERROR", ex.Message, ex));
        }
    }
    
    public async Task<MethodResult<Job>> ClaimNextJobForWorker(Guid workerId, CancellationToken cancellationToken)
    {
        // Implementation for claiming a job atomically
        // This is crucial for preventing duplicate processing
        throw new NotImplementedException("Custom implementation required for atomic claim operation");
    }

    public async Task<int> RecoverStuckJobs(long timeoutUnixTime, int maxRetries, CancellationToken cancellationToken)
    {
        try
        {
            // Recover jobs that have been in progress beyond the timeout
            // and haven't exceeded maxRetries
            var recoverTime = DateTimeOffset.FromUnixTimeSeconds(timeoutUnixTime);
            var sql = @"
                UPDATE Jobs 
                SET Status = @Status, 
                    WorkerId = NULL, 
                    RetryCount = RetryCount + 1,
                    LastUpdatedAt = @LastUpdatedAt
                WHERE Status = @InProgressStatus 
                  AND StartedAt < @RecoverTime
                  AND RetryCount < @MaxRetries";

            var rowsAffected = await _connection.ExecuteAsync(sql, new
            {
                Status = JobStatus.Queued.ToString(),
                LastUpdatedAt = recoverTime,
                InProgressStatus = JobStatus.InProgress.ToString(),
                RecoverTime = recoverTime,
                MaxRetries = maxRetries
            });

            return rowsAffected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recovering stuck jobs with timeoutUnixTime: {TimeoutUnixTime}", timeoutUnixTime);
            return 0; // Return 0 if recovery failed
        }
    }

    private string Serialize<T>(T obj)
    {
        return System.Text.Json.JsonSerializer.Serialize(obj);
    }
    
    private T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrEmpty(json)) return default;
        return System.Text.Json.JsonSerializer.Deserialize<T>(json);
    }
}
```

### Registering Custom Job Store

```csharp
// Register your custom job store
builder.Services.AddSingleton<IJobStore, CustomDatabaseJobStore>();

// Use the custom store
builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsWorker(); // Skip AddAsyncEndpointsInMemoryStore/AddAsyncEndpointsRedisStore
```

## Advanced Routing Patterns

### Multiple Routes for Same Handler

```csharp
// Process data with different priority levels
app.MapAsyncPost<HighPriorityRequest>("HighPriorityProcess", "/api/process/high");
app.MapAsyncPost<NormalPriorityRequest>("NormalPriorityProcess", "/api/process/normal");
app.MapAsyncPost<LowPriorityRequest>("LowPriorityProcess", "/api/process/low");

// Use the same handler with different job names
builder.Services.AddAsyncEndpointHandler<PriorityProcessorHandler, HighPriorityRequest, ProcessResult>("HighPriorityProcess");
builder.Services.AddAsyncEndpointHandler<PriorityProcessorHandler, NormalPriorityRequest, ProcessResult>("NormalPriorityProcess");
builder.Services.AddAsyncEndpointHandler<PriorityProcessorHandler, LowPriorityRequest, ProcessResult>("LowPriorityProcess");
```

### Dynamic Route Parameters

```csharp
// Route with dynamic parameters that are preserved
app.MapAsyncPost<DataRequest>("ProcessByUser", "/api/users/{userId}/process/{action}");

public class ProcessByUserHandler : IAsyncEndpointRequestHandler<DataRequest, ProcessResult>
{
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        var routeParams = context.RouteParams;
        var userId = routeParams.GetValueOrDefault("userId", null)?.ToString();
        var action = routeParams.GetValueOrDefault("action", null)?.ToString();
        
        logger.LogInformation("Processing {Action} for user {UserId}", action, userId);
        
        // Use route parameters in processing
        var result = await ProcessForUser(userId, action, context.Request);
        
        return MethodResult<ProcessResult>.Success(result);
    }
}
```

## Security Integration Patterns

### Authentication and Authorization

```csharp
// Add authentication to async endpoints
app.MapAsyncPost<DataRequest>("SecureProcess", "/api/secure-process")
    .RequireAuthorization("AsyncDataProcessor"); // Apply authorization policy

// Custom validation with authentication
app.MapAsyncPost<DataRequest>("SecureProcess", "/api/secure-process", 
    async (HttpContext context, DataRequest request, CancellationToken token) => 
    {
        // Custom authentication check
        if (!context.User.Identity.IsAuthenticated)
        {
            return Results.Unauthorized();
        }
        
        // Check specific claims
        if (!context.User.IsInRole("AsyncProcessor"))
        {
            return Results.Forbidden();
        }
        
        // Continue with processing
        return null;
    });
```

### Tenant Isolation

```csharp
// Preserve tenant information through the async pipeline
app.MapAsyncPost<TenantRequest>("TenantProcess", "/api/{tenantId}/process", 
    async (HttpContext context, TenantRequest request, CancellationToken token) => 
    {
        var tenantId = context.Request.RouteValues["tenantId"]?.ToString();
        
        // Validate tenant access
        if (!await ValidateTenantAccess(context, tenantId))
        {
            return Results.Forbidden();
        }
        
        // Add tenant to request if needed
        request.TenantId = tenantId;
        
        return null;
    });

public class TenantProcessHandler : IAsyncEndpointRequestHandler<TenantRequest, ProcessResult>
{
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<TenantRequest> context, CancellationToken token)
    {
        // Access tenant information from preserved context
        var routeParams = context.RouteParams;
        var tenantId = routeParams.GetValueOrDefault("tenantId", null)?.ToString();
        
        // Or from the request
        var request = context.Request;
        logger.LogInformation("Processing for tenant: {TenantId}", tenantId);
        
        return MethodResult<ProcessResult>.Success(new ProcessResult());
    }
}
```

## Performance Optimization Tips

### Efficient Job Batching

```csharp
// For operations that can be batched, configure appropriate batch sizes
builder.Services.AddAsyncEndpoints(options =>
{
    // Adjust batch sizes based on your work patterns
    options.WorkerConfigurations.BatchSize = 10; // Process jobs in batches
    options.JobManagerConfiguration.MaxClaimBatchSize = 20; // Claim jobs in larger batches
});
```

### Resource Management

```csharp
public class ResourceEfficientHandler(ILogger<ResourceEfficientHandler> logger, 
                                     IServiceProvider serviceProvider) 
    : IAsyncEndpointRequestHandler<DataRequest, ProcessResult>
{
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        // Use scoped services efficiently
        using var scope = serviceProvider.CreateScope();
        var specializedService = scope.ServiceProvider.GetRequiredService<ISpecializedService>();
        
        try
        {
            var result = await specializedService.ProcessAsync(context.Request, token);
            return MethodResult<ProcessResult>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing request for job {JobId}", context.Request);
            return MethodResult<ProcessResult>.Failure(ex);
        }
    }
}
```

## Advanced Monitoring and Observability

### Custom Metrics Collection

```csharp
public class MonitoredHandler(ILogger<MonitoredHandler> logger, 
                             IMetricsService metricsService) 
    : IAsyncEndpointRequestHandler<DataRequest, ProcessResult>
{
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        var startTime = DateTimeOffset.UtcNow;
        var jobId = context.RouteParams.GetValueOrDefault("jobId")?.ToString() ?? context.Request.GetHashCode().ToString();
        
        try
        {
            logger.LogInformation("Starting processing for job {JobId}", jobId);
            
            var result = await ProcessRequest(context.Request, token);
            
            var duration = DateTimeOffset.UtcNow - startTime;
            await metricsService.RecordProcessingTime(jobId, duration);
            
            logger.LogInformation("Completed processing for job {JobId} in {Duration}ms", 
                                jobId, duration.TotalMilliseconds);
            
            return MethodResult<ProcessResult>.Success(result);
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;
            await metricsService.RecordProcessingError(jobId, duration, ex.GetType().Name);
            
            logger.LogError(ex, "Error processing job {JobId} after {Duration}ms", 
                          jobId, duration.TotalMilliseconds);
            
            return MethodResult<ProcessResult>.Failure(ex);
        }
    }
}
```

These advanced features provide powerful capabilities for extending AsyncEndpoints functionality, implementing custom business logic, and integrating with existing systems while maintaining the core benefits of asynchronous processing.