---
sidebar_position: 14
---

# Performance & Optimization

## Overview

AsyncEndpoints is designed for high-performance background job processing. This guide covers optimization techniques to maximize throughput, minimize latency, and optimize resource usage in your async endpoints.

## Concurrency Configuration

### Worker Concurrency

Configure the maximum number of concurrent jobs per worker:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    // Set based on your system's capabilities
    options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount * 2;
    
    // For I/O bound operations, you can go higher
    // For CPU bound operations, keep closer to processor count
});
```

### Job Processing Batches

Adjust batch sizes for optimal performance:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.WorkerConfigurations.BatchSize = 10;  // Process up to 10 jobs per batch
    options.JobManagerConfiguration.MaxClaimBatchSize = 20;  // Claim up to 20 jobs at once
});
```

## Queue Management

### Queue Size Limits

Set appropriate queue size limits to prevent memory issues:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.WorkerConfigurations.MaximumQueueSize = 1000;  // Prevent memory overflow
});
```

### Polling Intervals

Configure polling intervals based on your workload:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.WorkerConfigurations.PollingIntervalMs = 100;   // Fast polling for high throughput
    options.JobManagerConfiguration.JobPollingIntervalMs = 500; // Balance between responsiveness and resource usage
});
```

## Storage Performance

### Redis Performance Optimization

For Redis storage, optimize connection and usage:

```csharp
// Use connection multiplexer for efficient connection management
var options = ConfigurationOptions.Parse("your-redis-host:6379");
options.AbortOnConnectFail = false;
options.ConnectRetry = 3;
options.ConnectTimeout = 5000;
options.ResponseTimeout = 5000;
options.KeepAlive = 180;

var connection = ConnectionMultiplexer.Connect(options);
builder.Services.AddAsyncEndpointsRedisStore(connection);
```

### Redis Key Patterns

Understand the Redis key structure for optimization:

- `ae:job:{jobId}`: Individual job data
- `ae:jobs:queue`: Sorted set for queue management
- `ae:jobs:inprogress`: Sorted set for active jobs

## Handler Optimization

### Efficient Handler Implementation

Write efficient handlers that don't block resources:

```csharp
public class OptimizedHandler : IAsyncEndpointRequestHandler<InputRequest, OutputResult>
{
    private readonly IMyService _myService;
    private readonly ILogger<OptimizedHandler> _logger;

    public OptimizedHandler(IMyService myService, ILogger<OptimizedHandler> logger)
    {
        _myService = myService;
        _logger = logger;
    }

    public async Task<MethodResult<OutputResult>> HandleAsync(AsyncContext<InputRequest> context, CancellationToken token)
    {
        // Use cancellation token in all async operations
        try
        {
            // Perform work efficiently
            var result = await _myService.ProcessAsync(context.Request, token);
            
            // Only access context data when needed
            var userId = context.Headers["X-User-Id"]?.FirstOrDefault();
            if (!string.IsNullOrEmpty(userId))
            {
                await LogUserActivity(userId, token);
            }
            
            return MethodResult<OutputResult>.Success(result);
        }
        catch (Exception ex)
        {
            return MethodResult<OutputResult>.Failure(ex);
        }
    }
}
```

### Memory Management

Avoid memory leaks in handlers:

```csharp
public async Task<MethodResult<LargerResult>> HandleAsync(AsyncContext<LargerRequest> context, CancellationToken token)
{
    // Use streaming for large data
    await using var stream = await _fileService.GetFileAsync(context.Request.FileId, token);
    
    // Process stream efficiently without loading everything into memory
    using var reader = new StreamReader(stream);
    var result = new LargerResult();
    
    string line;
    while ((line = await reader.ReadLineAsync(token)) != null)
    {
        // Process line by line
        result.ProcessLine(line);
    }
    
    return MethodResult<LargerResult>.Success(result);
}
```

## Serialization Optimization

### Efficient Serialization

Optimize JSON serialization for performance:

```csharp
// Create a custom JsonContext
[JsonSerializable(typeof(InputRequest))]
[JsonSerializable(typeof(OutputResult))]
[JsonSerializable(typeof(Job))]
public partial class OptimizedJsonContext : JsonSerializerContext
{
}

// Register the optimized context
builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsJsonTypeInfoResolver(OptimizedJsonContext.Default);
```

### Large Payload Handling

Handle large payloads efficiently:

```csharp
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<LargeRequest> context, CancellationToken token)
{
    // If dealing with very large payloads, consider:
    // 1. Storing large data in external storage
    // 2. Processing in chunks
    // 3. Using streaming approaches
    
    // For example, if the request data is very large:
    if (context.Request.Data.Length > 1000000) // 1MB threshold
    {
        // Store in external storage and pass reference
        var storageId = await _storageService.StoreAsync(context.Request.Data, token);
        var result = await ProcessLargePayload(storageId, token);
        await _storageService.CleanupAsync(storageId, token); // Cleanup after processing
        
        return MethodResult<ProcessResult>.Success(result);
    }
    
    // Process normally for smaller payloads
    var normalResult = await ProcessPayload(context.Request.Data, token);
    return MethodResult<ProcessResult>.Success(normalResult);
}
```

## Background Worker Performance

### Worker Configuration

Fine-tune worker settings for your workload:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    var config = options.WorkerConfigurations;
    
    // Adjust based on your workload characteristics
    config.MaximumConcurrency = GetOptimalConcurrency(); // Your logic to determine optimal value
    config.PollingIntervalMs = GetOptimalPollingInterval(); // Adjust based on job frequency
    config.BatchSize = 15; // Optimize based on job processing time
    config.JobTimeoutMinutes = 60; // Set appropriate for your longest jobs
});

private static int GetOptimalConcurrency()
{
    // I/O bound: higher concurrency
    // CPU bound: closer to processor count
    var isIoBound = true; // Determine based on your workload
    return isIoBound ? Environment.ProcessorCount * 4 : Environment.ProcessorCount;
}

private static int GetOptimalPollingInterval()
{
    // High frequency jobs: lower interval
    // Low frequency jobs: higher interval to save resources
    return 100; // milliseconds
}
```

### Recovery Configuration

Optimize distributed recovery settings:

```csharp
builder.Services.AddAsyncEndpointsWorker(recoveryConfiguration =>
{
    // Only enable if you have multi-instance deployment
    recoveryConfiguration.EnableDistributedJobRecovery = true;
    
    // Set timeout appropriately - not too short (false positives) or too long (delayed recovery)
    recoveryConfiguration.JobTimeoutMinutes = 30;
    
    // Check interval - balance between responsiveness and system load
    recoveryConfiguration.RecoveryCheckIntervalSeconds = 300; // 5 minutes
});
```

## Resource Management

### Memory Optimization

Monitor and optimize memory usage:

```csharp
public async Task<MethodResult<MemoryIntensiveResult>> HandleAsync(AsyncContext<MemoryIntensiveRequest> context, CancellationToken token)
{
    // Use memory-efficient collections
    List<string> results = new List<string>();
    
    // Process in chunks to manage memory
    const int chunkSize = 1000;
    for (int i = 0; i < context.Request.Items.Count; i += chunkSize)
    {
        var chunk = context.Request.Items
            .Skip(i)
            .Take(chunkSize)
            .ToList();
            
        var chunkResult = await ProcessChunk(chunk, token);
        results.AddRange(chunkResult);
        
        // Allow garbage collection
        if (token.IsCancellationRequested) break;
    }
    
    return MethodResult<MemoryIntensiveResult>.Success(new MemoryIntensiveResult { Results = results });
}
```

### Connection Pooling

For handlers that use external services, use connection pooling:

```csharp
public class ServiceUsingHandler : IAsyncEndpointRequestHandler<InputRequest, OutputResult>
{
    private readonly IHttpClientFactory _httpClientFactory; // Uses connection pooling
    private readonly ILogger<ServiceUsingHandler> _logger;

    public ServiceUsingHandler(IHttpClientFactory httpClientFactory, ILogger<ServiceUsingHandler> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<MethodResult<OutputResult>> HandleAsync(AsyncContext<InputRequest> context, CancellationToken token)
    {
        using var client = _httpClientFactory.CreateClient("MyApiClient");
        
        // Use the pooled connection
        var response = await client.PostAsJsonAsync("/api/process", context.Request, token);
        
        var result = await response.Content.ReadFromJsonAsync<OutputResult>(cancellationToken: token);
        return MethodResult<OutputResult>.Success(result);
    }
}
```

## Monitoring and Metrics

### Performance Monitoring

Add performance monitoring to your handlers:

```csharp
public async Task<MethodResult<OutputResult>> HandleAsync(AsyncContext<InputRequest> context, CancellationToken token)
{
    using var activity = MyActivitySource.StartActivity("ProcessRequest");
    
    var startTime = DateTime.UtcNow;
    _logger.LogInformation("Starting job {JobId} at {StartTime}", context.RouteParams["jobId"], startTime);

    try
    {
        var result = await ProcessRequest(context.Request, token);
        
        var duration = DateTime.UtcNow - startTime;
        _logger.LogInformation("Job {JobId} completed in {Duration}ms", 
            context.RouteParams["jobId"], duration.TotalMilliseconds);
        
        // Record metric
        JobProcessingDuration.Record(duration.TotalMilliseconds);
        
        return MethodResult<OutputResult>.Success(result);
    }
    catch (Exception ex)
    {
        var duration = DateTime.UtcNow - startTime;
        _logger.LogError(ex, "Job {JobId} failed after {Duration}ms", 
            context.RouteParams["jobId"], duration.TotalMilliseconds);
        
        // Record failure metric
        JobProcessingFailures.Add(1);
        throw;
    }
}

[Counter("async-endpoints.job-processing-duration", "Processing duration in milliseconds")]
public static Histogram<long> JobProcessingDuration = Meter.CreateHistogram<long>("async-endpoints.job-processing-duration");

[Counter("async-endpoints.job-processing-failures", "Number of job processing failures")]
public static Counter<long> JobProcessingFailures = Meter.CreateCounter<long>("async-endpoints.job-processing-failures");
```

## Load Testing

### Performance Testing Configuration

Set up performance testing for your async endpoints:

```csharp
// Example using a load testing tool configuration
public class PerformanceTestConfiguration
{
    public void ConfigureForLoadTesting(IServiceCollection services)
    {
        services.AddAsyncEndpoints(options =>
        {
            // For load testing, you might want different settings
            options.WorkerConfigurations.MaximumConcurrency = 50;
            options.WorkerConfigurations.PollingIntervalMs = 10; // More aggressive polling
            options.WorkerConfigurations.MaximumQueueSize = 5000; // Larger queue for load test
        });
        
        // Use in-memory store for faster load testing
        services.AddAsyncEndpointsInMemoryStore();
    }
}
```

## Profiling and Diagnostics

### Diagnostic Configuration

Enable diagnostic features for performance analysis:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    // For development/profiling, you might enable more verbose logging
    if (builder.Environment.IsDevelopment())
    {
        options.ResponseConfigurations.ExceptionResponseFactory = async (ex, context) =>
        {
            // Log more details for debugging
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Exception in async endpoint: {Exception}", ex);
            return Results.Problem(ex.Message, statusCode: 500);
        };
    }
});
```

## Performance Best Practices

### 1. Configure Based on Workload Type

- **I/O Bound**: Higher concurrency (4-8x processor count)
- **CPU Bound**: Concurrency near processor count
- **Mixed**: Balanced approach based on profiling

### 2. Monitor Key Metrics

- Job processing time
- Queue depth
- Worker utilization
- Memory usage
- Redis connection efficiency

### 3. Optimize for Your Use Case

- Batch processing for high-volume scenarios
- Individual processing for low-latency requirements  
- Appropriate timeout settings for your operations

### 4. Use Appropriate Storage

- In-memory for development and single-instance
- Redis for production and multi-instance deployments

### 5. Implement Circuit Breakers

Prevent system overload:

```csharp
// In validation middleware
app.MapAsyncPost<InputRequest>("Process", "/api/process",
    async (HttpContext context, InputRequest request, CancellationToken token) =>
    {
        var queueSize = await GetApproximateQueueSize(); // Custom method
        if (queueSize > 0.8 * MaximumQueueSize) // 80% threshold
        {
            return Results.Problem("System overloaded, try again later", statusCode: 503);
        }
        
        return null; // Continue
    });
```

### 6. Optimize Serialization

Use efficient serialization and avoid unnecessary data copying.

Performance optimization in AsyncEndpoints requires understanding your specific workload patterns and tuning the configuration accordingly. Monitor your system continuously and adjust settings based on actual performance metrics.