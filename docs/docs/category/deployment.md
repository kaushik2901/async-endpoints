---
sidebar_position: 15
---

# Deployment & Production

## Overview

Deploying AsyncEndpoints applications to production requires careful consideration of infrastructure, configuration, monitoring, and scalability. This guide covers best practices for production deployment.

## Infrastructure Requirements

### Redis Infrastructure (Production)

For production deployments, Redis is the recommended storage backend:

```yaml
# Example docker-compose.yml for Redis
version: '3.8'
services:
  redis:
    image: redis:7-alpine
    restart: unless-stopped
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    command: redis-server --appendonly yes --maxmemory 2gb --maxmemory-policy allkeys-lru
    environment:
      - REDIS_PASSWORD=${REDIS_PASSWORD}

volumes:
  redis-data:
```

### Connection Configuration

Configure Redis connections securely:

```csharp
// Use configuration from environment variables
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") 
    ?? throw new InvalidOperationException("Redis connection string is required in production");

builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsRedisStore(redisConnectionString)
    .AddAsyncEndpointsWorker();
```

In `appsettings.Production.json`:

```json
{
  "ConnectionStrings": {
    "Redis": "your-redis-host:6379,password=your-password,ssl=true,abortConnect=false"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "AsyncEndpoints": "Information"
    }
  }
}
```

## Production Configuration

### Worker Configuration

Optimize worker settings for production:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    // Production-specific settings
    options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount; // Conservative for production
    options.WorkerConfigurations.PollingIntervalMs = 1000; // Balance between responsiveness and resources
    options.WorkerConfigurations.JobTimeoutMinutes = 60; // Appropriate for your longest jobs
    options.WorkerConfigurations.MaximumQueueSize = 1000; // Prevent memory issues
    options.WorkerConfigurations.BatchSize = 10; // Optimize based on your workload

    // Job manager settings
    options.JobManagerConfiguration.DefaultMaxRetries = 3; // Balance between resilience and resource usage
    options.JobManagerConfiguration.RetryDelayBaseSeconds = 5.0; // Don't retry too quickly
    options.JobManagerConfiguration.MaxConcurrentJobs = 20; // Limit resource consumption
    options.JobManagerConfiguration.JobClaimTimeout = TimeSpan.FromMinutes(10); // Adjust based on job length
});
```

### Distributed Recovery

Configure distributed job recovery properly:

```csharp
builder.Services.AddAsyncEndpointsWorker(recoveryConfiguration =>
{
    recoveryConfiguration.EnableDistributedJobRecovery = true; // Essential for multi-instance deployments
    recoveryConfiguration.JobTimeoutMinutes = 60; // Set based on your longest expected job
    recoveryConfiguration.RecoveryCheckIntervalSeconds = 600; // 10 minutes - balance between responsiveness and overhead
    recoveryConfiguration.MaximumRetries = 2; // Additional retries for recovered jobs
});
```

## Deployment Strategies

### Single Instance Deployment

For single-instance deployments:

```csharp
builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsRedisStore(redisConnectionString) // Still use Redis for persistence
    .AddAsyncEndpointsWorker();

// Configure a single worker appropriately
builder.Services.Configure<AsyncEndpointsConfigurations>(options =>
{
    options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount;
});
```

### Multi-Instance Deployment

For multi-instance deployments, ensure each instance is properly configured:

```csharp
builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsRedisStore(redisConnectionString)
    .AddAsyncEndpointsWorker();

// Each instance will automatically get a unique WorkerId, but you can customize it
builder.Services.Configure<AsyncEndpointsConfigurations>(options =>
{
    // You can set a custom worker ID based on instance name or deployment information
    options.WorkerConfigurations.WorkerId = GetWorkerIdFromEnvironment();
});

private static Guid GetWorkerIdFromEnvironment()
{
    var instanceId = Environment.GetEnvironmentVariable("INSTANCE_ID");
    if (!string.IsNullOrEmpty(instanceId))
    {
        return Guid.TryParse(instanceId, out var guid) ? guid : Guid.NewGuid();
    }
    
    // Use machine name or other unique identifier
    return Guid.NewGuid();
}
```

## Monitoring and Health Checks

### Health Checks

Implement health checks for your async endpoints:

```csharp
// Custom health check for AsyncEndpoints
public class AsyncEndpointsHealthCheck : IHealthCheck
{
    private readonly IJobStore _jobStore;
    private readonly ILogger<AsyncEndpointsHealthCheck> _logger;

    public AsyncEndpointsHealthCheck(IJobStore jobStore, ILogger<AsyncEndpointsHealthCheck> logger)
    {
        _jobStore = jobStore;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Test basic connectivity
            var testJobId = Guid.NewGuid();
            var testResult = await _jobStore.GetJobById(testJobId, cancellationToken);
            
            // Verify Redis connectivity if using Redis
            if (_jobStore is RedisJobStore redisStore)
            {
                // Redis-specific health check
            }

            return HealthCheckResult.Healthy("AsyncEndpoints storage is accessible");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AsyncEndpoints health check failed");
            return HealthCheckResult.Unhealthy($"AsyncEndpoints storage error: {ex.Message}");
        }
    }
}

// Register in Program.cs
builder.Services.AddHealthChecks()
    .AddCheck<AsyncEndpointsHealthCheck>("async-endpoints", timeout: TimeSpan.FromSeconds(10));

// Map health check endpoint
app.MapHealthChecks("/health");
```

### Application Metrics

Add performance monitoring:

```csharp
// Add metrics collection
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
        metrics.AddMeter("AsyncEndpoints")
               .AddAspNetCoreInstrumentation()
               .AddRuntimeInstrumentation());

// In your handlers, add custom metrics
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<InputRequest> context, CancellationToken token)
{
    var startTime = DateTime.UtcNow;
    
    try
    {
        var result = await ProcessRequest(context.Request, token);
        
        var duration = DateTime.UtcNow - startTime;
        JobProcessingDuration.Record(duration.TotalMilliseconds, 
            new KeyValuePair<string, object?>("job.name", context.RouteParams["jobName"]?.ToString()));
        
        return MethodResult<ProcessResult>.Success(result);
    }
    catch (Exception ex)
    {
        JobProcessingFailures.Add(1, 
            new KeyValuePair<string, object?>("job.name", context.RouteParams["jobName"]?.ToString()));
        throw;
    }
}
```

## Logging Configuration

### Structured Logging

Configure structured logging for troubleshooting:

```csharp
// In Program.cs
builder.Logging.AddFilter("Microsoft", LogLevel.Warning); // Reduce noise
builder.Logging.AddFilter("AsyncEndpoints", LogLevel.Information);
builder.Logging.AddConsole(); // Add your preferred logging providers

// In your handlers
public class LoggingHandler : IAsyncEndpointRequestHandler<InputRequest, OutputResult>
{
    private readonly ILogger<LoggingHandler> _logger;

    public LoggingHandler(ILogger<LoggingHandler> logger)
    {
        _logger = logger;
    }

    public async Task<MethodResult<OutputResult>> HandleAsync(AsyncContext<InputRequest> context, CancellationToken token)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["JobId"] = context.RouteParams["jobId"],
            ["UserId"] = context.Headers["X-User-Id"]?.FirstOrDefault(),
            ["RequestDataSize"] = context.Request.Data?.Length ?? 0
        });

        _logger.LogInformation("Starting job processing for user {UserId}", 
            context.Headers["X-User-Id"]?.FirstOrDefault());

        try
        {
            var result = await ProcessRequest(context.Request, token);
            _logger.LogInformation("Job completed successfully");
            return MethodResult<OutputResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job processing failed");
            return MethodResult<OutputResult>.Failure(ex);
        }
    }
}
```

## Security Considerations

### Input Validation

Implement thorough input validation:

```csharp
app.MapAsyncPost<InputRequest>("Process", "/api/process",
    async (HttpContext context, InputRequest request, CancellationToken token) =>
    {
        // Security validations
        if (request.Data.Length > 1000000) // 1MB limit
        {
            return Results.BadRequest("Request data too large");
        }

        // Validate content type if needed
        var contentType = context.Request.Headers.ContentType.FirstOrDefault();
        if (!string.IsNullOrEmpty(contentType) && !contentType.StartsWith("application/json"))
        {
            return Results.BadRequest("Only JSON content type is accepted");
        }

        // Validate API key if required
        var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
        if (!await ValidateApiKeyAsync(apiKey))
        {
            return Results.Unauthorized();
        }

        return null; // Continue processing
    });
```

### Resource Limits

Implement resource limits to prevent abuse:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.WorkerConfigurations.MaximumQueueSize = 1000; // Prevent queue overflow
    options.JobManagerConfiguration.MaxConcurrentJobs = 50; // Limit concurrent processing
});
```

## Backup and Recovery

### Job Data Backup

For Redis, implement proper backup strategies:

```bash
# Redis backup configuration
# In redis.conf or via command line
save 900 1     # Save if at least 1 key changed in 15 minutes
save 300 10    # Save if at least 10 keys changed in 5 minutes  
save 60 10000  # Save if at least 10000 keys changed in 1 minute
```

### Disaster Recovery

Plan for disaster recovery scenarios:

```csharp
// Implement job recovery procedures
public class JobRecoveryService
{
    private readonly IJobStore _jobStore;
    private readonly ILogger<JobRecoveryService> _logger;

    public JobRecoveryService(IJobStore jobStore, ILogger<JobRecoveryService> logger)
    {
        _jobStore = jobStore;
        _logger = logger;
    }

    public async Task<int> RecoverStuckJobsAsync(int timeoutMinutes)
    {
        if (_jobStore.SupportsJobRecovery && _jobStore is IRecoverableJobStore recoverableStore)
        {
            var timeoutUnixTime = DateTimeOffset.UtcNow.AddMinutes(-timeoutMinutes).ToUnixTimeSeconds();
            var recoveredCount = await recoverableStore.RecoverStuckJobs(
                timeoutUnixTime, 
                3, // max retries
                CancellationToken.None);
            
            _logger.LogInformation("Recovered {Count} stuck jobs", recoveredCount);
            return recoveredCount;
        }
        
        return 0;
    }
}
```

## Scaling Strategies

### Horizontal Scaling

For horizontal scaling, ensure your configuration supports it:

```csharp
// Each instance should be configured the same way
builder.Services
    .AddAsyncEndpoints(options =>
    {
        // Shared configuration across all instances
        options.WorkerConfigurations.PollingIntervalMs = 1000;
        options.WorkerConfigurations.MaximumConcurrency = 5; // Lower concurrency per instance
        options.JobManagerConfiguration.JobClaimTimeout = TimeSpan.FromMinutes(5);
    })
    .AddAsyncEndpointsRedisStore(redisConnectionString)
    .AddAsyncEndpointsWorker(recoveryConfiguration =>
    {
        recoveryConfiguration.EnableDistributedJobRecovery = true;
        recoveryConfiguration.RecoveryCheckIntervalSeconds = 300;
    });
```

### Vertical Scaling

For vertical scaling, optimize resource utilization:

```csharp
// Adjust based on available resources
var cpuCount = Environment.ProcessorCount;
var availableMemoryGB = GetAvailableMemoryGB(); // Custom method

builder.Services.Configure<AsyncEndpointsConfigurations>(options =>
{
    // Scale concurrency with available CPU
    options.WorkerConfigurations.MaximumConcurrency = Math.Min(cpuCount * 2, 20);
    
    // Scale queue size with available memory
    options.WorkerConfigurations.MaximumQueueSize = (int)(availableMemoryGB * 100); // Custom formula
});
```

## Deployment Best Practices

### 1. Environment-Specific Configuration

Use different configurations for different environments:

```json
// appsettings.Production.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "AsyncEndpoints": "Information"
    }
  },
  "AsyncEndpoints": {
    "WorkerConfigurations": {
      "MaximumConcurrency": 10,
      "PollingIntervalMs": 1000
    }
  }
}
```

### 2. Graceful Shutdown

Ensure proper cleanup during shutdown:

```csharp
// The background service handles graceful shutdown internally
// But you can add custom shutdown logic if needed
app.Lifetime.ApplicationStopping.Register(() =>
{
    // Custom shutdown logic
    logger.LogInformation("Application is shutting down...");
});
```

### 3. Rolling Deployments

Support rolling deployments by:

- Using Redis for coordination between old and new instances
- Ensuring job claims timeout properly
- Testing the deployment process thoroughly

### 4. Configuration Validation

Validate configuration at startup:

```csharp
// Validate critical configuration at startup
app.Lifetime.ApplicationStarted.Register(async () =>
{
    using var scope = app.Services.CreateScope();
    var jobStore = scope.ServiceProvider.GetRequiredService<IJobStore>();
    
    try
    {
        // Test storage connectivity
        await jobStore.GetJobById(Guid.NewGuid(), CancellationToken.None);
        logger.LogInformation("AsyncEndpoints storage validation successful");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "AsyncEndpoints storage validation failed");
        // Consider shutting down if critical
    }
});
```

## Monitoring in Production

### Key Metrics to Monitor

Monitor these critical metrics:

- Job processing time
- Queue depth
- Worker utilization
- Error rates
- Retry frequencies
- Storage performance

### Alerting

Set up alerts for:

- High error rates (>5% of jobs failing)
- Queue build-up (queue size approaching limit)
- Worker unresponsiveness
- Storage connectivity issues

AsyncEndpoints applications can be successfully deployed to production with proper planning, configuration, and monitoring. The key is to start with conservative settings and adjust based on actual performance metrics and operational experience.