---
sidebar_position: 4
title: Performance
---

# Performance

This page covers performance optimization strategies for AsyncEndpoints applications, including configuration tuning, memory management, throughput optimization, and benchmarking approaches.

## Overview

AsyncEndpoints is designed for high performance in async processing scenarios. Performance optimization involves configuring workers, tuning system parameters, managing memory efficiently, and leveraging the right storage backend for your workload.

## Performance Optimization Strategies

### Concurrency Optimization

#### Worker Concurrency Configuration

The `MaximumConcurrency` setting is crucial for performance:

```csharp
// For CPU-bound operations, match to processor count
options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount;

// For I/O-bound operations, use higher concurrency
options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount * 2;

// For mixed workloads, use a balanced approach
options.WorkerConfigurations.MaximumConcurrency = Math.Min(Environment.ProcessorCount * 2, 16);
```

#### Concurrency Pattern Analysis

**CPU-Bound Operations:**
- Use concurrency equal to or slightly less than CPU cores
- Avoid oversubscription which can lead to context switching overhead
- Monitor CPU usage to find the optimal balance

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    // CPU-bound operations
    options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount;
    options.WorkerConfigurations.BatchSize = 1; // Process individually to avoid long-running CPU tasks
});
```

**I/O-Bound Operations:**
- Use higher concurrency since threads are often waiting for I/O
- Can exceed CPU count significantly without performance degradation
- Balance between throughput and resource usage

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    // I/O-bound operations
    options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount * 4;
    options.WorkerConfigurations.BatchSize = 5; // Process in batches for efficiency
    options.WorkerConfigurations.PollingIntervalMs = 1000; // Frequent checks for I/O completion
});
```

### Queue Size Optimization

#### Memory vs Throughput Balance

```csharp
// Small queue for memory-conscious environments
options.WorkerConfigurations.MaximumQueueSize = 50;

// Medium queue for balanced performance
options.WorkerConfigurations.MaximumQueueSize = 500;

// Large queue for high throughput
options.WorkerConfigurations.MaximumQueueSize = 2000;
```

#### Circuit Breaker Configuration

The queue size acts as a circuit breaker, preventing system overload:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    // Calculate queue size based on expected load
    var expectedJobsPerMinute = 1000;
    var averageProcessingTimeSeconds = 5;
    
    // Queue size = expected burst capacity
    var queueSize = (int)(expectedJobsPerMinute * (averageProcessingTimeSeconds / 60.0) * 2); // 2x safety margin
    options.WorkerConfigurations.MaximumQueueSize = queueSize;
});
```

### Polling Interval Optimization

#### Responsiveness vs Resource Usage

```csharp
// Fast polling (more responsive but higher resource usage)
options.WorkerConfigurations.PollingIntervalMs = 500;

// Standard polling (balanced approach)
options.WorkerConfigurations.PollingIntervalMs = 2000;

// Conservative polling (less resource usage but less responsive)
options.WorkerConfigurations.PollingIntervalMs = 10000;
```

#### Adaptive Polling

Implement adaptive polling based on queue state:

```csharp
// This would be implemented in a custom job producer
public class AdaptiveJobProducer : IJobProducerService
{
    private readonly ILogger<AdaptiveJobProducer> _logger;
    private readonly IJobStore _jobStore;
    private int _currentPollingInterval = 2000;
    
    public async Task ProduceJobsAsync(ChannelWriter<Job> writer, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Check queue depth to adjust polling
            var queueDepth = await _jobStore.GetQueueDepthAsync(cancellationToken);
            
            // Adjust polling based on queue depth
            if (queueDepth > 100) // High load
            {
                _currentPollingInterval = 500; // Faster polling
            }
            else if (queueDepth < 10) // Low load
            {
                _currentPollingInterval = 5000; // Slower polling
            }
            
            // Produce jobs with current interval
            await ProduceJobsOnce(writer, cancellationToken);
            
            await Task.Delay(_currentPollingInterval, cancellationToken);
        }
    }
}
```

## Memory Management

### Efficient Job Serialization

#### Minimize Payload Size

```csharp
// Custom serialization for efficiency
public class EfficientSerializer : ISerializer
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false // Smaller payloads
    };
    
    public string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, _options);
    }
    
    public T? Deserialize<T>(string value)
    {
        return JsonSerializer.Deserialize<T>(value, _options);
    }
}
```

#### Large Payload Handling

For large payloads, consider external storage:

```csharp
public class LargePayloadHandler : IAsyncEndpointRequestHandler<LargeDataRequest, ProcessResult>
{
    private readonly IFileStorageService _fileStorage;
    
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<LargeDataRequest> context, CancellationToken token)
    {
        var request = context.Request;
        
        // For large payloads, store externally and pass reference
        if (request.Data.Length > 1024 * 1024) // 1MB threshold
        {
            var fileId = await _fileStorage.StoreAsync(request.Data, token);
            var referenceRequest = new LargeDataReferenceRequest 
            { 
                FileId = fileId,
                Metadata = request.Metadata 
            };
            
            // Process with file reference instead of large payload
            return await ProcessWithFileReference(referenceRequest, token);
        }
        
        // Process normally for small payloads
        return await ProcessNormal(context.Request, token);
    }
}
```

### Batch Processing Efficiency

#### Optimized Batch Sizes

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    // For small, fast jobs - use larger batches
    options.WorkerConfigurations.BatchSize = 20;
    options.JobManagerConfiguration.MaxClaimBatchSize = 50;
    
    // For large, slow jobs - use smaller batches
    // options.WorkerConfigurations.BatchSize = 2;
    // options.JobManagerConfiguration.MaxClaimBatchSize = 5;
});
```

## Throughput Optimization

### Storage Performance

#### Redis Performance Configuration

```csharp
// Optimize Redis connection for performance
builder.Services.AddAsyncEndpointsRedisStore(config =>
{
    config.ConnectionString = "redis-server:6379";
    config.ConnectRetry = 3;
    config.ConnectTimeout = 5000;
    config.AbortOnConnectFail = false;
    
    // Performance-oriented settings
    config.Ssl = false; // Disable SSL for internal networks if security allows
});
```

#### In-Memory Optimization

For development and single-instance production:

```csharp
// In-memory store is already optimized for speed
// but monitor memory usage with large queues
builder.Services.AddAsyncEndpoints(options =>
{
    // Ensure memory limits are appropriate
    options.WorkerConfigurations.MaximumQueueSize = 100; // Smaller for memory constraints
    
    // Faster processing
    options.WorkerConfigurations.PollingIntervalMs = 100;
    options.JobManagerConfiguration.JobPollingIntervalMs = 100;
});
```

### Job Processing Optimization

#### Efficient Handler Implementation

```csharp
public class OptimizedHandler : IAsyncEndpointRequestHandler<DataRequest, ProcessResult>
{
    // Use injected services efficiently
    private readonly IMemoryCache _cache;
    private readonly ILogger<OptimizedHandler> _logger;
    private readonly IProcessorService _processorService;
    
    public OptimizedHandler(IMemoryCache cache, ILogger<OptimizedHandler> logger, IProcessorService processorService)
    {
        _cache = cache;
        _logger = logger;
        _processorService = processorService;
    }
    
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        var request = context.Request;
        
        try
        {
            // Use caching for expensive operations
            var cacheKey = $"processed_{request.Data.GetHashCode()}";
            if (_cache.TryGetValue(cacheKey, out ProcessResult cachedResult))
            {
                _logger.LogDebug("Cache hit for {CacheKey}", cacheKey);
                return MethodResult<ProcessResult>.Success(cachedResult);
            }
            
            // Process with injected service
            var result = await _processorService.ProcessAsync(request, token);
            
            // Cache result if appropriate
            if (result != null)
            {
                _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
            }
            
            return MethodResult<ProcessResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request {RequestId}", request.GetHashCode());
            return MethodResult<ProcessResult>.Failure(ex);
        }
    }
}
```

## Benchmarking Approaches

### Performance Testing Setup

```csharp
public class PerformanceTests
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    
    public PerformanceTests()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    // Configure for performance testing
                    services.Configure<AsyncEndpointsConfigurations>(config =>
                    {
                        config.WorkerConfigurations.MaximumConcurrency = 8;
                        config.WorkerConfigurations.BatchSize = 10;
                        config.WorkerConfigurations.MaximumQueueSize = 1000;
                        config.WorkerConfigurations.PollingIntervalMs = 100;
                    });
                });
            });
        
        _client = _factory.CreateClient();
    }
    
    [Fact]
    public async Task MeasureThroughput()
    {
        // Send multiple requests to measure throughput
        var requests = 100;
        var tasks = new Task<HttpResponseMessage>[requests];
        
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < requests; i++)
        {
            var request = new DataRequest { Data = $"TestData{i}", ProcessingPriority = 1 };
            tasks[i] = _client.PostAsJsonAsync("/api/process-data", request);
        }
        
        var responses = await Task.WhenAll(tasks);
        stopwatch.Stop();
        
        var successCount = responses.Count(r => r.IsSuccessStatusCode);
        var averageTime = stopwatch.ElapsedMilliseconds / (double)requests;
        
        // Log performance metrics
        Console.WriteLine($"Sent {requests} requests in {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Average time per request: {averageTime}ms");
        Console.WriteLine($"Success rate: {successCount}/{requests}");
        
        // Verify all requests were accepted
        Assert.Equal(requests, successCount);
    }
}
```

### Load Testing Configuration

```csharp
public class LoadTestConfiguration
{
    public static void ConfigureForLoadTesting(IServiceCollection services)
    {
        services.Configure<AsyncEndpointsConfigurations>(config =>
        {
            // Maximize throughput for testing
            config.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount * 4;
            config.WorkerConfigurations.BatchSize = 20;
            config.WorkerConfigurations.MaximumQueueSize = 10000;
            config.WorkerConfigurations.PollingIntervalMs = 50;
            
            // Optimistic retry settings for testing
            config.JobManagerConfiguration.DefaultMaxRetries = 0; // No retries during load tests
            config.JobManagerConfiguration.JobPollingIntervalMs = 50;
        });
    }
}
```

## Profiling Techniques

### Performance Profiling Setup

```csharp
public class PerformanceProfilingHandler : IAsyncEndpointRequestHandler<DataRequest, ProcessResult>
{
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        using var activity = ActivitySource.StartActivity("ProcessData", ActivityKind.Internal);
        
        // Capture timing information
        var startTime = DateTimeOffset.UtcNow;
        activity?.SetTag("request.start_time", startTime);
        
        try
        {
            var result = await ProcessRequestWithProfiling(context.Request, token, activity);
            
            var duration = DateTimeOffset.UtcNow - startTime;
            activity?.SetTag("request.duration_ms", duration.TotalMilliseconds);
            activity?.SetTag("result.status", "success");
            
            return MethodResult<ProcessResult>.Success(result);
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;
            activity?.SetTag("request.duration_ms", duration.TotalMilliseconds);
            activity?.SetTag("result.status", "error");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            return MethodResult<ProcessResult>.Failure(ex);
        }
    }
    
    private async Task<ProcessResult> ProcessRequestWithProfiling(DataRequest request, CancellationToken token, Activity? activity)
    {
        // Break down processing into measurable steps
        using var step1Activity = ActivitySource.StartActivity("ValidateRequest");
        await ValidateRequestAsync(request, token);
        step1Activity?.Dispose();
        
        using var step2Activity = ActivitySource.StartActivity("ProcessData");
        var result = await ProcessDataAsync(request, token);
        step2Activity?.Dispose();
        
        return result;
    }
}
```

### Memory Profiling

Monitor memory usage patterns:

```csharp
public class MemoryMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly Meter _meter = new("AsyncEndpoints.Performance");
    private static readonly Histogram<long> _memoryUsage = _meter.CreateHistogram<long>(
        "process.memory.usage", 
        unit: "bytes", 
        description: "Memory usage during request processing");
    
    public MemoryMonitoringMiddleware(RequestDelegate next)
    {
        _next = next;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        var beforeMemory = GC.GetTotalMemory(false);
        
        await _next(context);
        
        var afterMemory = GC.GetTotalMemory(false);
        _memoryUsage.Record(afterMemory - beforeMemory);
    }
}
```

## Production Performance Tuning

### Configuration for Production

```csharp
// Production-ready configuration
builder.Services.AddAsyncEndpoints(options =>
{
    // Conservative settings to prevent resource exhaustion
    options.WorkerConfigurations.MaximumConcurrency = Math.Min(Environment.ProcessorCount, 16);
    options.WorkerConfigurations.PollingIntervalMs = 2000; // Moderate frequency
    options.WorkerConfigurations.JobTimeoutMinutes = 60; // Reasonable timeout
    options.WorkerConfigurations.BatchSize = 5; // Balanced batch size
    options.WorkerConfigurations.MaximumQueueSize = 1000; // Circuit breaker protection
    
    // Retry settings for resilience
    options.JobManagerConfiguration.DefaultMaxRetries = 3;
    options.JobManagerConfiguration.RetryDelayBaseSeconds = 2.0;
    options.JobManagerConfiguration.MaxConcurrentJobs = 50;
    options.JobManagerConfiguration.JobPollingIntervalMs = 1000;
    options.JobManagerConfiguration.MaxClaimBatchSize = 10;
    options.JobManagerConfiguration.StaleJobClaimCheckInterval = TimeSpan.FromMinutes(1);
});
```

### Monitoring Performance Metrics

```csharp
public class PerformanceMetricsService
{
    private readonly Meter _meter = new("AsyncEndpoints");
    private readonly Histogram<double> _jobProcessingTime;
    private readonly Counter<long> _jobsProcessed;
    private readonly Counter<long> _jobsFailed;
    
    public PerformanceMetricsService()
    {
        _jobProcessingTime = _meter.CreateHistogram<double>(
            "async_endpoint.job.processing_time", 
            unit: "milliseconds",
            description: "Time taken to process jobs");
        
        _jobsProcessed = _meter.CreateCounter<long>(
            "async_endpoint.jobs.processed",
            description: "Number of successfully processed jobs");
        
        _jobsFailed = _meter.CreateCounter<long>(
            "async_endpoint.jobs.failed",
            description: "Number of failed jobs");
    }
    
    public async Task<MethodResult<T>> ExecuteWithMetrics<T>(
        string jobName,
        Func<Task<MethodResult<T>>> operation,
        CancellationToken token)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = await operation();
            
            if (result.IsSuccess)
            {
                _jobsProcessed.Add(1, new("job_name", jobName));
                _jobProcessingTime.Record(stopwatch.ElapsedMilliseconds, new("job_name", jobName));
            }
            else
            {
                _jobsFailed.Add(1, new("job_name", jobName));
            }
            
            return result;
        }
        finally
        {
            stopwatch.Stop();
        }
    }
}
```

### Performance Optimization Checklist

- **Concurrency**: Match to workload type (CPU vs I/O bound)
- **Queue Sizes**: Balance between throughput and memory usage
- **Polling Intervals**: Balance between responsiveness and resource usage
- **Batch Sizes**: Match to job characteristics (size and processing time)
- **Storage**: Choose appropriate backend (Redis for production, in-memory for development)
- **Timeouts**: Set appropriate values to prevent resource exhaustion
- **Caching**: Implement where appropriate to reduce processing time
- **Monitoring**: Track performance metrics to identify bottlenecks

## Troubleshooting Performance Issues

### Common Performance Bottlenecks

1. **Too High Concurrency**: Leading to context switching overhead
2. **Too Low Concurrency**: Leading to underutilization
3. **Inappropriate Queue Sizes**: Leading to memory issues or system overload
4. **Suboptimal Polling Intervals**: Leading to either high resource usage or poor responsiveness
5. **Large Payloads**: Leading to memory pressure and increased I/O
6. **Inefficient Handlers**: Leading to longer processing times

### Performance Monitoring

```csharp
// Add performance counters to your application
public class PerformanceMonitoringService : BackgroundService
{
    private readonly ILogger<PerformanceMonitoringService> _logger;
    private readonly IJobStore _jobStore;
    
    public PerformanceMonitoringService(ILogger<PerformanceMonitoringService> logger, IJobStore jobStore)
    {
        _logger = logger;
        _jobStore = jobStore;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Monitor queue depth
                var queueDepth = await GetQueueMetrics();
                
                _logger.LogInformation(
                    "Queue depth: {QueueDepth}, Processing: {ProcessingCount}, Completed: {CompletedCount}",
                    queueDepth.Queued, queueDepth.Processing, queueDepth.Completed);
                
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring performance");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
    
    private async Task<(int Queued, int Processing, int Completed) > GetQueueMetrics()
    {
        // Implementation to get queue metrics from your storage
        // This would be storage-specific
        return (0, 0, 0); // Placeholder
    }
}
```

Performance optimization for AsyncEndpoints requires understanding your specific workload characteristics and tuning the configuration accordingly. Regular monitoring and profiling help identify bottlenecks and ensure optimal performance.