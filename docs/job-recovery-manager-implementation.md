# Job Recovery Manager Implementation Guide

## Overview

This document provides a comprehensive guide for implementing the Job Recovery Manager feature in AsyncEndpoints. The Job Recovery Manager addresses the issue where jobs remain stuck in the `InProgress` state after system failures by implementing a distributed recovery mechanism.

## Problem Statement

The current AsyncEndpoints implementation lacks a mechanism to recover jobs that were in progress when a worker crashes or the system restarts. These jobs remain permanently stuck in the `InProgress` state and are never processed again.

### Current Issue Details
- Jobs claimed by a worker and moved to `InProgress` state remain stuck if the worker crashes
- No timeout-based recovery mechanism exists
- System restart does not trigger recovery of stuck jobs
- Manual intervention required to fix stuck jobs

## Solution Architecture

The Job Recovery Manager implements a worker-specific approach where recovery is enabled specifically when adding worker services. The implementation uses a distributed recovery approach that allows multiple worker instances to participate in job recovery without conflicts.

### Key Components
1. **DistributedJobRecoveryService** - Background service that periodically checks for stuck jobs
2. **Worker-specific configuration** - Recovery enabled via AddAsyncEndpointsWorker options
3. **IJobStore integration** - Job store determines if recovery is supported
4. **Atomic Operations** - Redis Lua scripts to ensure safe recovery across multiple instances

## Implementation Details

### 1. Recovery Configuration Class

Create a configuration class for recovery options:

```csharp
public class AsyncEndpointsRecoveryConfiguration
{
    public bool EnableDistributedJobRecovery { get; set; } = false;
    public int JobTimeoutMinutes { get; set; } = 30;
    public int RecoveryCheckIntervalSeconds { get; set; } = 300; // 5 minutes
    public int MaximumRetries { get; set; } = 3;
    public double RetryDelayBaseSeconds { get; set; } = 5.0;
}
```

### 2. IJobStore Method for Recovery Support

Add a method to the IJobStore interface to check if recovery is supported:

```csharp
public interface IJobStore
{
    // Existing methods...
    
    /// <summary>
    /// Determines if this job store implementation supports job recovery
    /// </summary>
    bool SupportsJobRecovery { get; }
}
```

### 3. JobStore Implementation for Recovery Support

Update existing job store implementations:

For InMemoryJobStore:
```csharp
public class InMemoryJobStore : IJobStore
{
    // Existing implementation...
    
    public bool SupportsJobRecovery => false; // In-memory store doesn't support recovery
}
```

For RedisJobStore:
```csharp
public class RedisJobStore : IJobStore
{
    // Existing implementation...
    
    public bool SupportsJobRecovery => true; // Redis supports recovery
    
    // Recovery-specific methods added later in this document
}
```

### 4. Updated Extension Method with Recovery Options

Modify the extension method to accept recovery configuration:

```csharp
// In src/AsyncEndpoints/Extensions/ServiceCollectionExtensions.cs

/// <summary>
/// Adds the background worker services required to process async jobs.
/// This includes job consumers, producers, processors, and the hosted background service.
/// </summary>
/// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
/// <param name="recoveryConfiguration">Optional configuration for distributed job recovery.</param>
/// <returns>The <see cref="IServiceCollection"/> for method chaining.</returns>
public static IServiceCollection AddAsyncEndpointsWorker(this IServiceCollection services, 
    Action<AsyncEndpointsRecoveryConfiguration>? recoveryConfiguration = null)
{
    // Configure recovery options
    var recoveryConfig = new AsyncEndpointsRecoveryConfiguration();
    recoveryConfiguration?.Invoke(recoveryConfig);
    
    // Register recovery configuration as singleton
    services.AddSingleton(recoveryConfig);

    // Register worker services
    services.AddTransient<IJobConsumerService, JobConsumerService>();
    services.AddTransient<IJobProducerService, JobProducerService>();
    services.AddTransient<IJobProcessorService, JobProcessorService>();
    services.AddTransient<IJobChannelEnqueuer, JobChannelEnqueuer>();
    services.AddTransient<IHandlerExecutionService, HandlerExecutionService>();
    services.AddTransient<IDelayCalculatorService, DelayCalculatorService>();
    services.AddTransient<IJobClaimingService, JobClaimingService>();
    
    // Conditionally register recovery service based on configuration
    if (recoveryConfig.EnableDistributedJobRecovery)
    {
        services.AddHostedService<DistributedJobRecoveryService>();
    }

    return services;
}
```

### 5. DistributedJobRecoveryService Class

Create the recovery service that uses the recovery configuration:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.Configuration;
using AsyncEndpoints.Infrastructure;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.Background
{
    /// <summary>
    /// Background service that recovers stuck jobs that were in progress during system failures.
    /// This service is enabled when AddAsyncEndpointsWorker is called with recovery configuration.
    /// </summary>
    public class DistributedJobRecoveryService : BackgroundService
    {
        private readonly ILogger<DistributedJobRecoveryService> _logger;
        private readonly IJobStore _jobStore;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly TimeSpan _recoveryInterval;
        private readonly int _jobTimeoutMinutes;
        private readonly int _maxRetries;
        private readonly double _retryDelayBaseSeconds;

        public DistributedJobRecoveryService(
            ILogger<DistributedJobRecoveryService> logger,
            IJobStore jobStore,
            IDateTimeProvider dateTimeProvider,
            AsyncEndpointsRecoveryConfiguration recoveryConfig)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _jobStore = jobStore ?? throw new ArgumentNullException(nameof(jobStore));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            
            _recoveryInterval = TimeSpan.FromSeconds(recoveryConfig.RecoveryCheckIntervalSeconds);
            _jobTimeoutMinutes = recoveryConfig.JobTimeoutMinutes;
            _maxRetries = recoveryConfig.MaximumRetries;
            _retryDelayBaseSeconds = recoveryConfig.RetryDelayBaseSeconds;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_jobStore.SupportsJobRecovery)
            {
                _logger.LogWarning("Job Recovery Service is enabled but current job store does not support recovery. Service will not start.");
                return;
            }

            _logger.LogInformation("Job Recovery Service starting with timeout {Timeout} minutes and check interval {Interval} seconds", 
                _jobTimeoutMinutes, _recoveryInterval.TotalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RecoverStuckJobs(stoppingToken);
                    await Task.Delay(_recoveryInterval, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during job recovery cycle");
                    // Continue despite errors to keep the recovery process running
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }

            _logger.LogInformation("Job Recovery Service stopped");
        }

        private async Task RecoverStuckJobs(CancellationToken cancellationToken)
        {
            if (!_jobStore.SupportsJobRecovery)
            {
                return; // Should not happen if we checked in ExecuteAsync, but being safe
            }

            if (_jobStore is RedisJobStore redisJobStore)
            {
                var timeoutUnixTime = _dateTimeProvider.UtcNow.AddMinutes(-_jobTimeoutMinutes).ToUnixTimeSeconds();
                var recoveredCount = await redisJobStore.RecoverStuckJobs(
                    timeoutUnixTime, 
                    _maxRetries, 
                    _retryDelayBaseSeconds, 
                    cancellationToken);

                if (recoveredCount > 0)
                {
                    _logger.LogInformation("Recovered {RecoveredCount} stuck jobs", recoveredCount);
                }
                else
                {
                    _logger.LogDebug("No stuck jobs found during recovery check");
                }
            }
            else
            {
                _logger.LogWarning("Job store does not support recovery or recovery method not implemented: {StoreType}", _jobStore.GetType().Name);
            }
        }
    }
}
```

### 6. RedisJobStore Extension for Recovery

Extend the RedisJobStore class to support recovery operations:

```csharp
// In RedisJobStore.cs - Add this method:

public async Task<int> RecoverStuckJobs(
    long timeoutUnixTime, 
    int maxRetries, 
    double retryDelayBaseSeconds,
    CancellationToken cancellationToken)
{
    var luaScript = @"
        local timeoutUnixTime = tonumber(ARGV[1])
        local maxRetries = tonumber(ARGV[2])
        local retryDelayBaseSeconds = tonumber(ARGV[3])
        local currentTime = ARGV[4]
        
        local recoveredCount = 0
        local cursor = 0
        
        -- Use SCAN to efficiently find all job keys
        repeat
            local result = redis.call('SCAN', cursor, 'MATCH', 'ae:job:*', 'COUNT', 100)
            cursor = tonumber(result[1])
            local keys = result[2]
            
            for _, jobKey in ipairs(keys) do
                local status = redis.call('HGET', jobKey, 'Status')
                local startedAtUnix = redis.call('HGET', jobKey, 'StartedAtUnix')
                local retryCount = redis.call('HGET', jobKey, 'RetryCount') or '0'
                local maxRetriesForJob = redis.call('HGET', jobKey, 'MaxRetries') or ARGV[2]
                
                -- Check if job is InProgress (status 300) and started more than timeout ago
                if status == '300' and startedAtUnix then -- 300 = JobStatus.InProgress
                    if tonumber(startedAtUnix) < timeoutUnixTime then
                        retryCount = tonumber(retryCount)
                        maxRetriesForJob = tonumber(maxRetriesForJob)
                        
                        if retryCount < maxRetriesForJob then
                            -- Calculate exponential backoff delay
                            local newRetryCount = retryCount + 1
                            local newRetryDelay = math.pow(2, newRetryCount) * retryDelayBaseSeconds
                            local retryUntil = tonumber(currentTime) + newRetryDelay
                            
                            -- Update the job to scheduled status
                            redis.call('HSET', jobKey, 
                                'Status', '200', -- 200 = JobStatus.Scheduled
                                'RetryCount', tostring(newRetryCount),
                                'RetryDelayUntil', tostring(retryUntil),
                                'WorkerId', '', -- Release worker assignment
                                'StartedAt', '', -- Clear started time
                                'StartedAtUnix', '', -- Clear started time
                                'LastUpdatedAt', currentTime)
                            
                            -- Add back to the queue with the retry time as score
                            local jobId = string.gsub(jobKey, 'ae:job:', '')
                            redis.call('ZADD', 'ae:jobs:queue', retryUntil, jobId)
                            
                            recoveredCount = recoveredCount + 1
                        else
                            -- Mark as permanently failed
                            redis.call('HSET', jobKey,
                                'Status', '500', -- 500 = JobStatus.Failed
                                'Error', 'Job failed after maximum retries',
                                'WorkerId', '',
                                'StartedAt', '',
                                'StartedAtUnix', '',
                                'LastUpdatedAt', currentTime)
                        end
                    end
                end
            until cursor == 0
            
            return recoveredCount
    ";

    var result = await _database.ScriptEvaluateAsync(luaScript, 
        values: new RedisValue[]
        {
            timeoutUnixTime.ToString(),
            maxRetries.ToString(),
            retryDelayBaseSeconds.ToString(),
            _dateTimeProvider.UtcNow.ToUnixTimeSeconds().ToString()
        });

    return (int)(long)result;
}
```

### 7. Update Job Claiming Logic to Support Recovery

Modify the job claiming logic to store the Unix timestamp for efficient recovery:

```csharp
// In the existing ClaimNextJobForWorker Lua script within RedisJobStore, 
// add the StartedAtUnix field when updating job status to InProgress:

-- In the existing Lua script for claiming jobs, when setting status to InProgress:
local currentTime = ARGV[6]  -- assuming this contains the current time as Unix timestamp
local startedAtValue = ARGV[7]  -- ISO 8601 datetime string

redis.call('HSET', jobKey, 
    'Status', '300', -- JobStatus.InProgress
    'WorkerId', newWorkerId,
    'StartedAt', startedAtValue,
    'StartedAtUnix', currentTime,  -- Add Unix timestamp for efficient recovery
    'LastUpdatedAt', currentTime)
```



## Usage in User Applications

### For Worker Applications

Users enable recovery by passing configuration when adding worker services:

```csharp
// In Program.cs or Startup.cs of the user's worker application
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAsyncEndpoints() // Core services
    .AddAsyncEndpointsRedisStore("redis-connection-string") // Or InMemory store
    .AddAsyncEndpointsWorker(options => // Worker services with recovery configuration
    {
        options.EnableDistributedJobRecovery = true;
        options.JobTimeoutMinutes = 30; // Jobs timeout after 30 minutes
        options.RecoveryCheckIntervalSeconds = 300; // Check every 5 minutes
        options.MaximumRetries = 3;
        options.RetryDelayBaseSeconds = 5.0;
    }); // Recovery service automatically registered when enabled

var app = builder.Build();

app.Run();
```

### For Multiple Worker Instances

When deploying multiple worker instances:
- Each instance will automatically run the recovery service if enabled via the worker configuration
- The distributed recovery approach ensures safe operation across instances
- Multiple instances provide redundancy for the recovery process
- No coordination required between instances

## Configuration Options

| Option | Description | Default Value | Notes |
|--------|-------------|---------------|-------|
| `EnableDistributedJobRecovery` | Whether to enable the recovery service | false | Default is disabled for backwards compatibility |
| `JobTimeoutMinutes` | Time after which a job in progress is considered stuck | 30 | Should be longer than expected job processing time |
| `RecoveryCheckIntervalSeconds` | How often the recovery service checks for stuck jobs | 300 (5 minutes) | Balance between responsiveness and resource usage |
| `MaximumRetries` | Maximum number of times to retry a failed job | 3 | |
| `RetryDelayBaseSeconds` | Base delay in exponential backoff for retries | 5.0 | |

## Implementation Concerns

### 1. **Worker-Only Feature**
The recovery functionality is now specifically tied to worker applications through the `AddAsyncEndpointsWorker` method, which is the correct approach since recovery only makes sense in worker contexts.

### 2. **Conditional Service Registration**
The recovery service is only registered when explicitly enabled in the worker configuration, avoiding unnecessary services in applications that don't need recovery.

### 3. **Job Store Compatibility**
The service checks `SupportsJobRecovery` property to ensure it only runs when the current job store supports recovery operations.

### 4. **Redis-Only Implementation**
The recovery functionality currently only works with RedisJobStore. The InMemoryJobStore sets `SupportsJobRecovery = false`, which prevents recovery operations but allows applications to still use worker functionality.

### 5. **Clean API Design**
This approach keeps the API clean - users only need to add recovery configuration when calling `AddAsyncEndpointsWorker`, rather than managing separate extension methods.

## Testing Strategy

### Unit Tests
- Test recovery logic with mock job store
- Verify timeout calculations
- Test service behavior when recovery is disabled
- Test `SupportsJobRecovery` flag handling

### Integration Tests
- Test with actual Redis instance
- Simulate worker crashes and verify recovery
- Test with multiple recovery service instances

### End-to-End Tests
- Deploy multiple worker instances
- Submit jobs and simulate failures
- Verify recovery behavior across instances

## Monitoring and Observability

### Logging
- Log when recovery service starts (enabled/disabled)
- Log recovery cycles and recovered job counts
- Log errors during recovery operations
- Provide detailed logs for troubleshooting

### Metrics
- Track recovery operation frequency
- Monitor recovered job counts
- Measure recovery processing time

## Deployment Considerations

### Production Deployment
- Enable recovery service when calling `AddAsyncEndpointsWorker` in worker instances
- Configure appropriate timeout values based on job types
- Monitor resource usage of recovery operations
- Consider the impact of multiple instances on Redis load

### Development Deployment
- Recovery disabled by default for simpler development
- Can be enabled for testing recovery functionality

### Scaling Considerations
- Recovery service scales with worker instances
- Each instance performs recovery checks independently
- Redis load increases with the number of recovery instances
- Monitor Redis performance with multiple recovery services

## Troubleshooting

### Common Issues
1. **Recovery not working**: Check that `AddAsyncEndpointsWorker` was called with recovery enabled
2. **High Redis load**: Adjust recovery check intervals based on Redis capacity
3. **Jobs not being recovered**: Verify `StartedAtUnix` field is being populated when jobs are claimed

### Debugging Steps
1. Verify Redis connection and permissions
2. Check that `StartedAtUnix` field is being populated when jobs are claimed
3. Review logs for recovery service status messages
4. Validate that `AddAsyncEndpointsWorker` was called with proper configuration

## Security Considerations

- The recovery service uses the same Redis connection as the main job store
- No additional security risks introduced
- Ensure Redis connection is secured in production

## Summary

This worker-specific approach provides a clean solution where job recovery is enabled through the `AddAsyncEndpointsWorker` method with specific options. This ensures that recovery functionality is only available in worker contexts where it makes sense, and users have clear control over when to enable it.