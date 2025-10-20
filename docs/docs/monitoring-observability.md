---
sidebar_position: 4
title: Monitoring and Observability
---

# Monitoring and Observability

This page covers comprehensive monitoring and observability patterns for AsyncEndpoints applications, including structured logging, metrics collection, health checks, distributed tracing, and alerting strategies.

## Overview

Monitoring and observability are critical for production AsyncEndpoints applications. This section covers how to implement comprehensive logging, metrics, health checks, and tracing to ensure your async processing system remains reliable and performant.

## Structured Logging

### Enhanced Logging in Handlers

```csharp
public class MonitoredDataProcessingHandler(
    ILogger<MonitoredDataProcessingHandler> logger,
    AppDbContext dbContext,
    ITelemetryService telemetryService) 
    : IAsyncEndpointRequestHandler<DataProcessingRequest, DataProcessingResult>
{
    public async Task<MethodResult<DataProcessingResult>> HandleAsync(AsyncContext<DataProcessingRequest> context, CancellationToken token)
    {
        var request = context.Request;
        
        using var activity = ActivitySource.StartActivity("DataProcessing", ActivityKind.Internal);
        activity?.SetTag("request.category", request.Category);
        activity?.SetTag("request.start_date", request.StartDate?.ToString("O"));
        activity?.SetTag("request.end_date", request.EndDate?.ToString("O"));
        activity?.SetTag("request.user_id", context.Headers.GetValueOrDefault("X-User-Id", new List<string?>())?.FirstOrDefault());
        
        var startTime = DateTimeOffset.UtcNow;
        var jobId = context.RouteParams.GetValueOrDefault("jobId")?.ToString() ?? Guid.NewGuid().ToString();
        
        logger.LogInformation(
            "Starting data processing for job {JobId}, category: {Category}, date range: {StartDate} to {EndDate}",
            jobId, request.Category, request.StartDate, request.EndDate);
        
        try
        {
            // Add custom dimensions to the activity
            activity?.SetTag("job.id", jobId);
            
            // Process the data with monitoring
            var entities = await dbContext.DataEntities
                .Where(e => e.Category == request.Category && e.CreatedAt >= request.StartDate)
                .ToListAsync(token);
            
            logger.LogDebug("Fetched {EntityCount} entities for processing", entities.Count);
            
            // Process entities with progress tracking
            var processedEntities = new List<ProcessedDataEntity>();
            var totalEntities = entities.Count;
            var processedCount = 0;
            
            foreach (var entity in entities)
            {
                var processed = new ProcessedDataEntity
                {
                    OriginalId = entity.Id,
                    ProcessedData = entity.Data.ToUpper(),
                    ProcessedAt = DateTime.UtcNow,
                    Status = "Processed"
                };
                
                processedEntities.Add(processed);
                
                processedCount++;
                
                // Log progress every 100 entities
                if (processedCount % 100 == 0)
                {
                    logger.LogInformation(
                        "Processing progress: {ProcessedCount}/{TotalCount} for job {JobId}",
                        processedCount, totalEntities, jobId);
                    
                    // Check for cancellation periodically
                    token.ThrowIfCancellationRequested();
                }
            }
            
            // Save results
            await dbContext.ProcessedData.AddRangeAsync(processedEntities, token);
            await dbContext.SaveChangesAsync(token);
            
            var duration = DateTimeOffset.UtcNow - startTime;
            
            var result = new DataProcessingResult
            {
                ProcessedCount = processedEntities.Count,
                ProcessedAt = DateTime.UtcNow,
                Summary = $"Processed {processedEntities.Count} entities in {duration.TotalSeconds:F2}s"
            };
            
            logger.LogInformation(
                "Completed data processing for job {JobId} in {Duration}ms, {ProcessedCount} entities processed",
                jobId, duration.TotalMilliseconds, processedEntities.Count);
            
            // Record telemetry
            await telemetryService.RecordProcessingMetricsAsync(
                new ProcessingMetrics
                {
                    JobId = jobId,
                    Category = request.Category,
                    ProcessedCount = processedEntities.Count,
                    DurationMs = duration.TotalMilliseconds,
                    Timestamp = startTime
                });
            
            activity?.SetTag("result.processed_count", processedEntities.Count);
            activity?.SetTag("result.duration_ms", duration.TotalMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Ok);
            
            return MethodResult<DataProcessingResult>.Success(result);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            var duration = DateTimeOffset.UtcNow - startTime;
            logger.LogInformation(
                "Data processing was cancelled for job {JobId} after {Duration}ms",
                jobId, duration.TotalMilliseconds);
            
            activity?.SetStatus(ActivityStatusCode.Error, "Operation was cancelled");
            activity?.SetTag("error.type", "OperationCancelled");
            
            return MethodResult<DataProcessingResult>.Failure(
                AsyncEndpointError.FromCode("OPERATION_CANCELLED", "Processing was cancelled")
            );
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;
            logger.LogError(
                ex,
                "Error during data processing for job {JobId} after {Duration}ms",
                jobId, duration.TotalMilliseconds);
            
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);
            
            // Send error to telemetry
            await telemetryService.RecordErrorAsync(
                new ErrorMetrics
                {
                    JobId = jobId,
                    Category = request.Category,
                    ErrorType = ex.GetType().Name,
                    ErrorMessage = ex.Message,
                    DurationMs = duration.TotalMilliseconds,
                    Timestamp = startTime
                });
            
            return MethodResult<DataProcessingResult>.Failure(ex);
        }
    }
}
```

### Structured Log Configuration

```csharp
// Configure structured logging in Program.cs
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddJsonConsole(options =>
{
    options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss.fff zzz]";
    options.UseUtcTimestamp = true;
});

// Add Application Insights if configured
if (!string.IsNullOrEmpty(builder.Configuration["ApplicationInsights:ConnectionString"]))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

// Configure Serilog for more advanced logging
builder.Host.UseSerilog((context, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "AsyncEndpoints")
        .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
        .WriteTo.Console(outputTemplate: 
            "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File("logs/asyncendpoints-.txt", 
            rollingInterval: RollingInterval.Day));
```

### Custom Logging Enrichment

```csharp
public class JobContextLoggingHandler : IAsyncEndpointRequestHandler<DataRequest, ProcessResult>
{
    private readonly ILogger<JobContextLoggingHandler> _logger;
    
    public JobContextLoggingHandler(ILogger<JobContextLoggingHandler> logger)
    {
        _logger = logger;
    }
    
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        // Extract contextual information and add to logging scope
        using var _ = _logger.BeginScope(new
        {
            JobId = context.RouteParams.GetValueOrDefault("jobId", Guid.NewGuid().ToString()),
            UserId = context.Headers.GetValueOrDefault("X-User-Id", new List<string?>())?.FirstOrDefault(),
            RequestId = context.Headers.GetValueOrDefault("X-Request-Id", new List<string?>())?.FirstOrDefault(),
            ClientIP = context.Headers.GetValueOrDefault("X-Forwarded-For", new List<string?>())?.FirstOrDefault(),
            UserAgent = context.Headers.GetValueOrDefault("User-Agent", new List<string?>())?.FirstOrDefault()
        });
        
        try
        {
            _logger.LogInformation("Processing request with contextual information");
            
            // Perform actual processing
            var result = await ProcessAsync(context.Request, token);
            
            _logger.LogInformation("Request processed successfully");
            
            return MethodResult<ProcessResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during request processing with contextual information");
            return MethodResult<ProcessResult>.Failure(ex);
        }
    }
    
    private async Task<ProcessResult> ProcessAsync(DataRequest request, CancellationToken token)
    {
        // Processing logic here
        await Task.Delay(100, token); // Simulate work
        return new ProcessResult { ProcessedData = request.Data.ToUpper() };
    }
}
```

## Metrics Collection

### Custom Metrics Service

```csharp
public interface IMetricsService
{
    Task RecordJobMetricsAsync(JobMetrics metrics, CancellationToken token = default);
    Task RecordErrorMetricsAsync(ErrorMetrics metrics, CancellationToken token = default);
    Task<Dictionary<string, double>> GetMetricsAsync(CancellationToken token = default);
}

public class MetricsService : IMetricsService
{
    private readonly Meter _meter;
    private readonly Histogram<double> _jobProcessingTime;
    private readonly Counter<long> _jobsProcessed;
    private readonly Counter<long> _jobsFailed;
    private readonly Counter<long> _jobsQueued;
    private readonly Histogram<double> _jobQueueTime;
    private readonly ILogger<MetricsService> _logger;
    
    public MetricsService(ILogger<MetricsService> logger)
    {
        _logger = logger;
        _meter = new Meter("AsyncEndpoints", "1.0.0");
        
        _jobProcessingTime = _meter.CreateHistogram<double>(
            "async_job.processing_time", 
            unit: "milliseconds",
            description: "Time taken to process jobs");
            
        _jobsProcessed = _meter.CreateCounter<long>(
            "async_job.processed",
            description: "Number of successfully processed jobs");
            
        _jobsFailed = _meter.CreateCounter<long>(
            "async_job.failed",
            description: "Number of failed jobs");
            
        _jobsQueued = _meter.CreateCounter<long>(
            "async_job.queued",
            description: "Number of jobs queued");
            
        _jobQueueTime = _meter.CreateHistogram<double>(
            "async_job.queue_time",
            unit: "milliseconds",
            description: "Time jobs spend in queue");
    }
    
    public async Task RecordJobMetricsAsync(JobMetrics metrics, CancellationToken token = default)
    {
        try
        {
            _jobProcessingTime.Record(metrics.DurationMs, 
                new("job_type", metrics.JobType),
                new("worker_id", metrics.WorkerId));
                
            _jobsProcessed.Add(1, 
                new("job_type", metrics.JobType),
                new("worker_id", metrics.WorkerId));
                
            _jobQueueTime.Record(metrics.QueueTimeMs, 
                new("job_type", metrics.JobType));
                
            // Store in metrics store for aggregation
            await StoreMetricsAsync(metrics, token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording job metrics");
        }
    }
    
    public async Task RecordErrorMetricsAsync(ErrorMetrics metrics, CancellationToken token = default)
    {
        try
        {
            _jobsFailed.Add(1,
                new("error_type", metrics.ErrorType),
                new("job_type", metrics.JobType));
                
            // Additional error-specific metrics
            await StoreErrorMetricsAsync(metrics, token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording error metrics");
        }
    }
    
    public async Task<Dictionary<string, double>> GetMetricsAsync(CancellationToken token = default)
    {
        // Return aggregated metrics
        return new Dictionary<string, double>
        {
            ["jobs_processed"] = 100, // Actual implementation would query metrics store
            ["jobs_failed"] = 5,
            ["avg_processing_time"] = 1500.0,
            ["queue_size"] = 10
        };
    }
    
    private async Task StoreMetricsAsync(JobMetrics metrics, CancellationToken token)
    {
        // Implementation to store metrics in a metrics database (Prometheus, etc.)
        await Task.CompletedTask; // Placeholder
    }
    
    private async Task StoreErrorMetricsAsync(ErrorMetrics metrics, CancellationToken token)
    {
        // Implementation to store error metrics
        await Task.CompletedTask; // Placeholder
    }
}

public class JobMetrics
{
    public string JobId { get; set; } = string.Empty;
    public string JobType { get; set; } = string.Empty;
    public string WorkerId { get; set; } = string.Empty;
    public double DurationMs { get; set; }
    public double QueueTimeMs { get; set; }
    public DateTime Timestamp { get; set; }
}

public class ErrorMetrics
{
    public string JobId { get; set; } = string.Empty;
    public string JobType { get; set; } = string.Empty;
    public string ErrorType { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public double DurationMs { get; set; }
    public DateTime Timestamp { get; set; }
}
```

### Metrics-Enhanced Handler

```csharp
public class MetricsEnhancedHandler(
    ILogger<MetricsEnhancedHandler> logger,
    IMetricsService metricsService,
    IDataProcessor dataProcessor) 
    : IAsyncEndpointRequestHandler<DataRequest, ProcessResult>
{
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        var request = context.Request;
        var startTime = DateTimeOffset.UtcNow;
        
        // Record queued metric
        await metricsService.RecordJobMetricsAsync(new JobMetrics
        {
            JobId = context.RouteParams.GetValueOrDefault("jobId", Guid.NewGuid().ToString())?.ToString() ?? string.Empty,
            JobType = "DataRequest",
            WorkerId = "NotAssignedYet", // Will be assigned when processing starts
            DurationMs = 0, // Not known yet
            QueueTimeMs = 0, // Calculated later
            Timestamp = startTime
        }, token);
        
        try
        {
            var processingStart = DateTimeOffset.UtcNow;
            
            var result = await dataProcessor.ProcessAsync(request, token);
            
            var totalDuration = DateTimeOffset.UtcNow - startTime;
            var processingDuration = DateTimeOffset.UtcNow - processingStart;
            
            await metricsService.RecordJobMetricsAsync(new JobMetrics
            {
                JobId = context.RouteParams.GetValueOrDefault("jobId", Guid.NewGuid().ToString())?.ToString() ?? string.Empty,
                JobType = "DataRequest",
                WorkerId = Environment.MachineName, // Or actual worker ID
                DurationMs = processingDuration.TotalMilliseconds,
                QueueTimeMs = (processingStart - startTime).TotalMilliseconds,
                Timestamp = DateTimeOffset.UtcNow
            }, token);
            
            logger.LogInformation(
                "Job completed successfully in {TotalTime}ms (queue: {QueueTime}ms, processing: {ProcessTime}ms)",
                totalDuration.TotalMilliseconds,
                (processingStart - startTime).TotalMilliseconds,
                processingDuration.TotalMilliseconds);
            
            return MethodResult<ProcessResult>.Success(result);
        }
        catch (Exception ex)
        {
            var errorDuration = DateTimeOffset.UtcNow - startTime;
            
            await metricsService.RecordErrorMetricsAsync(new ErrorMetrics
            {
                JobId = context.RouteParams.GetValueOrDefault("jobId", Guid.NewGuid().ToString())?.ToString() ?? string.Empty,
                JobType = "DataRequest",
                ErrorType = ex.GetType().Name,
                ErrorMessage = ex.Message,
                DurationMs = errorDuration.TotalMilliseconds,
                Timestamp = DateTimeOffset.UtcNow
            }, token);
            
            logger.LogError(
                ex,
                "Job failed after {Duration}ms: {ErrorMessage}",
                errorDuration.TotalMilliseconds,
                ex.Message);
            
            return MethodResult<ProcessResult>.Failure(ex);
        }
    }
}
```

## Health Checks and Monitoring

### Comprehensive Health Check Implementation

```csharp
public class AsyncEndpointsHealthCheck : IHealthCheck
{
    private readonly IJobStore _jobStore;
    private readonly IJobManager _jobManager;
    private readonly ILogger<AsyncEndpointsHealthCheck> _logger;
    private readonly IOptions<AsyncEndpointsConfigurations> _configOptions;
    
    public AsyncEndpointsHealthCheck(
        IJobStore jobStore, 
        IJobManager jobManager, 
        ILogger<AsyncEndpointsHealthCheck> logger,
        IOptions<AsyncEndpointsConfigurations> configOptions)
    {
        _jobStore = jobStore;
        _jobManager = jobManager;
        _logger = logger;
        _configOptions = configOptions;
    }
    
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Test job store connectivity
            var testStartTime = DateTimeOffset.UtcNow;
            await _jobStore.GetJobById(Guid.NewGuid(), cancellationToken);
            var storeLatency = DateTimeOffset.UtcNow - testStartTime;
            
            // Check if job store is healthy
            if (storeLatency.TotalMilliseconds > 1000) // 1 second threshold
            {
                return HealthCheckResult.Degraded(
                    "Job store response time is too slow",
                    new Dictionary<string, object>
                    {
                        ["store_latency_ms"] = storeLatency.TotalMilliseconds,
                        ["threshold_ms"] = 1000
                    });
            }
            
            // Check queue depth metrics
            var stuckJobThreshold = _configOptions.Value.JobManagerConfiguration.StaleJobClaimCheckInterval;
            var stuckJobs = await GetStuckJobsCount(cancellationToken);
            
            if (stuckJobs > 10) // More than 10 stuck jobs is concerning
            {
                return HealthCheckResult.Degraded(
                    "Too many stuck jobs detected",
                    new Dictionary<string, object>
                    {
                        ["stuck_jobs_count"] = stuckJobs,
                        ["threshold"] = 10
                    });
            }
            
            // Check if we can submit a test job
            var testJobResult = await _jobManager.SubmitJob(
                "HealthCheck", 
                "{}", 
                CreateTestHttpContext(), 
                cancellationToken);
            
            if (!testJobResult.IsSuccess)
            {
                return HealthCheckResult.Unhealthy(
                    "Cannot submit test job",
                    new Dictionary<string, object>
                    {
                        ["error"] = testJobResult.Error?.Message
                    });
            }
            
            return HealthCheckResult.Healthy(
                "AsyncEndpoints is healthy", 
                new Dictionary<string, object>
                {
                    ["store_latency_ms"] = storeLatency.TotalMilliseconds,
                    ["stuck_jobs"] = stuckJobs,
                    ["max_retries"] = _configOptions.Value.JobManagerConfiguration.DefaultMaxRetries
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return HealthCheckResult.Unhealthy(
                "Health check failed", 
                ex,
                new Dictionary<string, object>
                {
                    ["error_type"] = ex.GetType().Name
                });
        }
    }
    
    private async Task<int> GetStuckJobsCount(CancellationToken cancellationToken)
    {
        // Implementation to count stuck jobs
        // This would be specific to your storage implementation
        if (_jobStore is RedisJobStore redisStore)
        {
            // For Redis, you might have a method to find stalled jobs
            // This is a placeholder implementation
            return 0;
        }
        
        // For other stores, implement accordingly
        return 0;
    }
    
    private HttpContext CreateTestHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["User-Agent"] = "AsyncEndpoints-HealthCheck";
        return context;
    }
}

// Add custom health check tags
public class QueueDepthHealthCheck : IHealthCheck
{
    private readonly IJobStore _jobStore;
    
    public QueueDepthHealthCheck(IJobStore jobStore)
    {
        _jobStore = jobStore;
    }
    
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var queueDepth = await GetQueueDepthAsync(cancellationToken);
        var healthy = queueDepth < 100; // Consider healthy if less than 100 items in queue
        
        return healthy ? 
            HealthCheckResult.Healthy($"Queue depth: {queueDepth}") : 
            HealthCheckResult.Degraded($"Queue depth too high: {queueDepth}", new { queue_depth = queueDepth });
    }
    
    private async Task<int> GetQueueDepthAsync(CancellationToken cancellationToken)
    {
        // Implementation to get current queue depth
        // This would be storage-specific
        return 0; // Placeholder
    }
}
```

### Health Check Configuration

```csharp
// In Program.cs
builder.Services.AddHealthChecks()
    .AddCheck<AsyncEndpointsHealthCheck>("asyncendpoints", timeout: TimeSpan.FromSeconds(10))
    .AddCheck<QueueDepthHealthCheck>("asyncendpoints.queuedepth", timeout: TimeSpan.FromSeconds(5), tags: new[] { "ready" })
    .AddDbContextCheck<AppDbContext>()
    .AddRedis(builder.Configuration.GetConnectionString("Redis"), name: "redis")
    .AddProcessAllocatedMemoryHealthCheck(maximumMegabytes: 500);

// Map health check endpoints with extended responses
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        var result = JsonSerializer.Serialize(
            new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(entry => new
                {
                    name = entry.Key,
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    duration = entry.Value.Duration,
                    tags = entry.Value.Tags,
                    data = entry.Value.Data
                }),
                totalDuration = report.TotalDuration
            },
            new JsonSerializerOptions { WriteIndented = true });

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(result);
    }
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

## Distributed Tracing

### OpenTelemetry Configuration

```csharp
// In Program.cs for OpenTelemetry setup
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddSource("AsyncEndpoints") // Add the ActivitySource name
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .SetResourceBuilder(
                ResourceBuilder.CreateDefault()
                    .AddService("AsyncEndpoints", serviceVersion: "1.0.0")
                    .AddAttributes(new[]
                    {
                        new KeyValuePair<string, object>("environment", builder.Environment.EnvironmentName),
                        new KeyValuePair<string, object>("host", Environment.MachineName)
                    }));
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter("AsyncEndpoints") // Add the Meter name
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
    });

// Add OpenTelemetry exporter (Jaeger, Zipkin, etc.)
if (builder.Environment.IsDevelopment())
{
    // For development, use console exporter
    builder.Services.Configure<OpenTelemetryLoggerOptions>(options =>
    {
        options.AddConsoleExporter();
    });
}
```

### Tracing-Enabled Handler

```csharp
public class TracingEnabledHandler(
    ILogger<TracingEnabledHandler> logger,
    IHttpClientFactory httpClientFactory,
    AppDbContext dbContext) 
    : IAsyncEndpointRequestHandler<TracingRequest, TracingResult>
{
    private static readonly ActivitySource ActivitySource = new("AsyncEndpoints.Tracing");
    
    public async Task<MethodResult<TracingResult>> HandleAsync(AsyncContext<TracingRequest> context, CancellationToken token)
    {
        using var activity = ActivitySource.StartActivity("ProcessTracingRequest", ActivityKind.Internal);
        
        // Add context information to the trace
        activity?.SetTag("request.id", context.RouteParams.GetValueOrDefault("requestId")?.ToString());
        activity?.SetTag("user.id", context.Headers.GetValueOrDefault("X-User-Id", new List<string?>())?.FirstOrDefault());
        activity?.SetTag("request.data_size", context.Request.Data?.Length ?? 0);
        
        var startTime = DateTimeOffset.UtcNow;
        
        try
        {
            logger.LogInformation("Processing tracing request with distributed tracing");
            
            // Step 1: Database operation
            using var dbActivity = ActivitySource.StartActivity("DatabaseOperation", ActivityKind.Internal);
            var entities = await dbContext.DataEntities
                .Where(e => e.Category == context.Request.Category)
                .ToListAsync(token);
            dbActivity?.SetTag("fetched_entities", entities.Count);
            dbActivity?.SetStatus(ActivityStatusCode.Ok);
            
            // Step 2: External API call
            using var apiActivity = ActivitySource.StartActivity("ExternalApiCall", ActivityKind.Client);
            apiActivity?.SetTag("http.method", "POST");
            apiActivity?.SetTag("http.url", "https://api.example.com/data");
            
            using var httpClient = httpClientFactory.CreateClient();
            var apiResponse = await httpClient.PostAsJsonAsync("https://api.example.com/data", entities, token);
            
            apiActivity?.SetTag("http.status_code", (int)apiResponse.StatusCode);
            apiActivity?.SetStatus(ActivityStatusCode.Ok);
            
            // Step 3: Process response
            var processedData = await ProcessApiResponseAsync(apiResponse, token);
            
            var result = new TracingResult
            {
                ProcessedData = processedData,
                ProcessedAt = DateTime.UtcNow,
                ExecutionTime = DateTimeOffset.UtcNow - startTime
            };
            
            // Add final tags to parent activity
            activity?.SetTag("result.processed_count", processedData.Count);
            activity?.SetTag("result.duration_ms", (DateTimeOffset.UtcNow - startTime).TotalMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Ok);
            
            logger.LogInformation("Tracing request completed successfully");
            
            return MethodResult<TracingResult>.Success(result);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);
            
            logger.LogError(ex, "Error during tracing request");
            
            return MethodResult<TracingResult>.Failure(ex);
        }
    }
    
    private async Task<List<string>> ProcessApiResponseAsync(HttpResponseMessage response, CancellationToken token)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new ExternalServiceException($"API call failed: {response.StatusCode}");
        }
        
        var content = await response.Content.ReadAsStringAsync(token);
        var data = JsonSerializer.Deserialize<List<string>>(content);
        
        return data ?? new List<string>();
    }
}
```

## Performance Monitoring

### Performance Counters and Metrics

```csharp
public class PerformanceMonitoringHandler(
    ILogger<PerformanceMonitoringHandler> logger,
    IMetricsService metricsService) 
    : IAsyncEndpointRequestHandler<PerformanceRequest, PerformanceResult>
{
    private readonly Meter _meter = new("AsyncEndpoints.Performance");
    private readonly Histogram<double> _processingTime;
    private readonly Counter<long> _processedItems;
    private readonly Histogram<double> _memoryUsage;
    
    public PerformanceMonitoringHandler(ILogger<PerformanceMonitoringHandler> logger, IMetricsService metricsService)
    {
        _logger = logger;
        _metricsService = metricsService;
        
        _processingTime = _meter.CreateHistogram<double>(
            "performance.processing_time",
            unit: "milliseconds",
            description: "Processing time for performance requests");
            
        _processedItems = _meter.CreateCounter<long>(
            "performance.processed_items",
            description: "Number of items processed");
            
        _memoryUsage = _meter.CreateHistogram<double>(
            "performance.memory_usage",
            unit: "bytes",
            description: "Memory usage during processing");
    }
    
    public async Task<MethodResult<PerformanceResult>> HandleAsync(AsyncContext<PerformanceRequest> context, CancellationToken token)
    {
        var request = context.Request;
        var startTime = DateTimeOffset.UtcNow;
        var startMemory = GC.GetTotalMemory(false);
        
        try
        {
            logger.LogInformation("Starting performance-intensive operation");
            
            // Simulate performance-intensive work
            var result = await ProcessPerformanceIntensiveTaskAsync(request, token);
            
            var duration = DateTimeOffset.UtcNow - startTime;
            var endMemory = GC.GetTotalMemory(false);
            var memoryUsed = endMemory - startMemory;
            
            // Record performance metrics
            _processingTime.Record(duration.TotalMilliseconds, new("task_type", request.TaskType));
            _processedItems.Add(result.ProcessedItemCount, new("task_type", request.TaskType));
            _memoryUsage.Record(memoryUsed, new("task_type", request.TaskType));
            
            // Also record in metrics service for external collection
            await metricsService.RecordJobMetricsAsync(new JobMetrics
            {
                JobId = context.RouteParams.GetValueOrDefault("jobId", Guid.NewGuid().ToString())?.ToString() ?? string.Empty,
                JobType = "Performance",
                WorkerId = Environment.MachineName,
                DurationMs = duration.TotalMilliseconds,
                QueueTimeMs = 0, // Could calculate this if needed
                Timestamp = startTime
            }, token);
            
            logger.LogInformation(
                "Performance operation completed in {Duration}ms, memory used: {MemoryUsed} bytes",
                duration.TotalMilliseconds, memoryUsed);
            
            return MethodResult<PerformanceResult>.Success(result);
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;
            logger.LogError(ex, "Performance operation failed after {Duration}ms", duration.TotalMilliseconds);
            
            await metricsService.RecordErrorMetricsAsync(new ErrorMetrics
            {
                JobId = context.RouteParams.GetValueOrDefault("jobId", Guid.NewGuid().ToString())?.ToString() ?? string.Empty,
                JobType = "Performance",
                ErrorType = ex.GetType().Name,
                ErrorMessage = ex.Message,
                DurationMs = duration.TotalMilliseconds,
                Timestamp = startTime
            }, token);
            
            return MethodResult<PerformanceResult>.Failure(ex);
        }
    }
    
    private async Task<PerformanceResult> ProcessPerformanceIntensiveTaskAsync(PerformanceRequest request, CancellationToken token)
    {
        // Simulate CPU-intensive work
        var result = new PerformanceResult
        {
            ProcessedItemCount = request.ItemCount,
            ProcessedAt = DateTime.UtcNow
        };
        
        // CPU-intensive operation
        var data = new List<int>(request.ItemCount);
        for (int i = 0; i < request.ItemCount; i++)
        {
            data.Add(i * i); // Simple calculation
            if (i % 10000 == 0) // Check cancellation periodically
            {
                token.ThrowIfCancellationRequested();
            }
        }
        
        // Simulate I/O if needed
        if (request.IncludeIoSimulation)
        {
            await Task.Delay(100, token); // Simulate I/O
        }
        
        result.ProcessedItemCount = data.Count;
        
        return result;
    }
}
```

## Alerting Strategies

### Alert Configuration

```csharp
public class AlertingService
{
    private readonly ILogger<AlertingService> _logger;
    private readonly ITelemetryService _telemetryService;
    private readonly IAlertSender _alertSender;
    
    // Alert thresholds
    private static readonly double PROCESSING_TIME_THRESHOLD = 5000; // 5 seconds
    private static readonly int FAILED_JOBS_THRESHOLD = 10; // per hour
    private static readonly int STUCK_JOBS_THRESHOLD = 5; // stuck for more than 10 minutes
    private static readonly double MEMORY_USAGE_THRESHOLD = 0.8; // 80% of allocated memory
    
    public AlertingService(
        ILogger<AlertingService> logger, 
        ITelemetryService telemetryService,
        IAlertSender alertSender)
    {
        _logger = logger;
        _telemetryService = telemetryService;
        _alertSender = alertSender;
    }
    
    public async Task CheckAlertsAsync(CancellationToken token)
    {
        // Check for slow processing jobs
        await CheckSlowProcessingJobs(token);
        
        // Check for high failure rates
        await CheckHighFailureRates(token);
        
        // Check for stuck jobs
        await CheckStuckJobs(token);
        
        // Check for memory usage
        await CheckMemoryUsage(token);
    }
    
    private async Task CheckSlowProcessingJobs(CancellationToken token)
    {
        var slowJobs = await _telemetryService.GetSlowProcessingJobsAsync(
            TimeSpan.FromSeconds(30), // Jobs taking more than 30 seconds
            token);
        
        if (slowJobs.Count > 0)
        {
            var message = $"Detected {slowJobs.Count} jobs with processing time exceeding threshold";
            
            await _alertSender.SendAlertAsync(new Alert
            {
                AlertType = AlertType.Performance,
                Severity = AlertSeverity.High,
                Title = "Slow Job Processing Detected",
                Message = message,
                Details = slowJobs.Select(j => new { j.JobId, j.DurationMs }).ToList()
            });
            
            _logger.LogWarning(message);
        }
    }
    
    private async Task CheckHighFailureRates(CancellationToken token)
    {
        var failureRate = await _telemetryService.GetFailureRateAsync(
            TimeSpan.FromHours(1), // Last hour
            token);
        
        if (failureRate > 0.1) // More than 10% failure rate
        {
            var message = $"High failure rate detected: {(failureRate * 100):F2}% in the last hour";
            
            await _alertSender.SendAlertAsync(new Alert
            {
                AlertType = AlertType.Error,
                Severity = AlertSeverity.High,
                Title = "High Job Failure Rate",
                Message = message,
                Details = new { failure_rate = failureRate }
            });
            
            _logger.LogWarning(message);
        }
    }
    
    private async Task CheckStuckJobs(CancellationToken token)
    {
        var stuckJobs = await _telemetryService.GetStuckJobsAsync(
            TimeSpan.FromMinutes(10), // Jobs stuck for more than 10 minutes
            token);
        
        if (stuckJobs.Count > STUCK_JOBS_THRESHOLD)
        {
            var message = $"Detected {stuckJobs.Count} stuck jobs exceeding threshold";
            
            await _alertSender.SendAlertAsync(new Alert
            {
                AlertType = AlertType.System,
                Severity = AlertSeverity.Medium,
                Title = "Stuck Jobs Detected",
                Message = message,
                Details = stuckJobs.Select(j => new { j.JobId, j.Status }).ToList()
            });
            
            _logger.LogWarning(message);
        }
    }
    
    private async Task CheckMemoryUsage(CancellationToken token)
    {
        var memoryUsage = (double)GC.GetTotalMemory(false) / GetMemoryLimit();
        
        if (memoryUsage > MEMORY_USAGE_THRESHOLD)
        {
            var message = $"Memory usage {(memoryUsage * 100):F2}% exceeds threshold of {(MEMORY_USAGE_THRESHOLD * 100):F2}%";
            
            await _alertSender.SendAlertAsync(new Alert
            {
                AlertType = AlertType.System,
                Severity = AlertSeverity.High,
                Title = "High Memory Usage",
                Message = message,
                Details = new { memory_usage_percent = memoryUsage * 100 }
            });
            
            _logger.LogWarning(message);
        }
    }
    
    private long GetMemoryLimit()
    {
        // Implementation to get the memory limit for the current process
        // This could be from environment variables, configuration, or system limits
        return Environment.WorkingSet; // Simplified implementation
    }
}

public class AlertingBackgroundService : BackgroundService
{
    private readonly ILogger<AlertingBackgroundService> _logger;
    private readonly AlertingService _alertingService;
    
    public AlertingBackgroundService(
        ILogger<AlertingBackgroundService> logger,
        AlertingService alertingService)
    {
        _logger = logger;
        _alertingService = alertingService;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Alerting service is starting");
        
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await CheckAndSendAlerts(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Check every 5 minutes
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in alerting service");
        }
        
        _logger.LogInformation("Alerting service is stopping");
    }
    
    private async Task CheckAndSendAlerts(CancellationToken token)
    {
        try
        {
            await _alertingService.CheckAlertsAsync(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking alerts");
        }
    }
}
```

## Monitoring Dashboard Integration

### Metrics Endpoint for External Monitoring

```csharp
// Add a custom metrics endpoint for Prometheus or other monitoring tools
app.MapGet("/metrics", async (IMetricsService metricsService) =>
{
    var metrics = await metricsService.GetMetricsAsync(CancellationToken.None);
    
    var sb = new StringBuilder();
    
    // Prometheus format
    foreach (var metric in metrics)
    {
        sb.AppendLine($"# TYPE asyncendpoints_{metric.Key} gauge");
        sb.AppendLine($"asyncendpoints_{metric.Key} {metric.Value}");
    }
    
    return Results.Text(sb.ToString(), "text/plain");
});

// Custom endpoint for detailed job metrics
app.MapGet("/metrics/jobs", async (IJobStore jobStore) =>
{
    var metrics = new JobMetricsSummary
    {
        QueuedJobs = await GetQueuedJobCount(jobStore),
        ProcessingJobs = await GetProcessingJobCount(jobStore),
        CompletedJobs = await GetCompletedJobCount(jobStore),
        FailedJobs = await GetFailedJobCount(jobStore),
        AvgProcessingTime = await GetAverageProcessingTime(jobStore),
        ErrorRate = await GetErrorRate(jobStore)
    };
    
    return Results.Json(metrics);
});

public class JobMetricsSummary
{
    public int QueuedJobs { get; set; }
    public int ProcessingJobs { get; set; }
    public int CompletedJobs { get; set; }
    public int FailedJobs { get; set; }
    public double AvgProcessingTime { get; set; } // in milliseconds
    public double ErrorRate { get; set; } // percentage
}
```

## Service Registration and Configuration

```csharp
// Complete service registration for monitoring and observability
builder.Services.AddSingleton<IMetricsService, MetricsService>();
builder.Services.AddSingleton<IAlertSender, EmailAlertSender>(); // or other implementation
builder.Services.AddSingleton<AlertingService>();

// Register background service for alerting
builder.Services.AddHostedService<AlertingBackgroundService>();

// Register health checks
builder.Services.AddScoped<AsyncEndpointsHealthCheck>();
builder.Services.AddScoped<QueueDepthHealthCheck>();

// Register the tracing-enabled handlers
builder.Services.AddAsyncEndpointHandler<MonitoredDataProcessingHandler, DataProcessingRequest, DataProcessingResult>("MonitoredProcess");
builder.Services.AddAsyncEndpointHandler<TracingEnabledHandler, TracingRequest, TracingResult>("TracedProcess");
builder.Services.AddAsyncEndpointHandler<PerformanceMonitoringHandler, PerformanceRequest, PerformanceResult>("PerformanceProcess");

// Configure AsyncEndpoints with monitoring
builder.Services.AddAsyncEndpoints(options =>
{
    // Add monitoring-enhanced response factories
    options.ResponseConfigurations.JobSubmittedResponseFactory = async (job, context) =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Job {JobId} submitted successfully", job.Id);
        
        // Add monitoring headers
        context.Response.Headers.Append("X-Async-Job-Id", job.Id.ToString());
        context.Response.Headers.Append("X-Processing-Time", DateTimeOffset.UtcNow.ToString("O"));
        
        return Results.Accepted($"/jobs/{job.Id}", job);
    };
    
    options.ResponseConfigurations.JobStatusResponseFactory = async (jobResult, context) =>
    {
        if (jobResult.IsSuccess && jobResult.Data != null)
        {
            var job = jobResult.Data;
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            
            logger.LogInformation(
                "Job {JobId} status requested: {Status}, completed: {CompletedAt}",
                job.Id, job.Status, job.CompletedAt);
            
            return Results.Ok(job);
        }
        
        return Results.NotFound();
    };
})
.AddAsyncEndpointsRedisStore(builder.Configuration.GetConnectionString("Redis"))
.AddAsyncEndpointsWorker();
```

The monitoring and observability patterns demonstrate how to implement comprehensive tracking, logging, metrics collection, health checks, and alerting for AsyncEndpoints applications. These patterns help ensure your async processing system remains reliable, performant, and maintainable in production environments.