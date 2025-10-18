---
sidebar_position: 6
title: Distributed Recovery Configuration
---

# Distributed Recovery Configuration

This page details the configuration for distributed job recovery in AsyncEndpoints, which enables automatic recovery of stuck jobs in multi-instance deployments.

## Overview

Distributed recovery is a critical feature for production environments where multiple application instances are running. It automatically detects and recovers jobs that have become stuck due to worker failures, network issues, or other unexpected events.

## Distributed Recovery Configuration Properties

### EnableDistributedJobRecovery
- **Type**: `bool`
- **Default**: `true`
- **Description**: Enables or disables the distributed job recovery mechanism
- **Impact**: Controls whether stuck jobs are automatically recovered

```csharp
// Enable distributed recovery (default)
recoveryConfiguration.EnableDistributedJobRecovery = true;

// Disable distributed recovery (not recommended for production)
recoveryConfiguration.EnableDistributedJobRecovery = false;
```

### JobTimeoutMinutes
- **Type**: `int`
- **Default**: `30` (30 minutes)
- **Description**: Timeout in minutes after which a job in progress is considered stuck
- **Impact**: Determines how long to wait before considering a job as needing recovery

```csharp
// Short timeout for fast operations
recoveryConfiguration.JobTimeoutMinutes = 10;

// Standard timeout (default)
recoveryConfiguration.JobTimeoutMinutes = 30;

// Long timeout for long-running operations
recoveryConfiguration.JobTimeoutMinutes = 120;
```

### RecoveryCheckIntervalSeconds
- **Type**: `int`
- **Default**: `300` (5 minutes)
- **Description**: Interval in seconds between recovery checks
- **Impact**: Affects how frequently the system checks for stuck jobs

```csharp
// Frequent checks for fast recovery
recoveryConfiguration.RecoveryCheckIntervalSeconds = 60; // 1 minute

// Standard interval (default)
recoveryConfiguration.RecoveryCheckIntervalSeconds = 300; // 5 minutes

// Less frequent checks to reduce resource usage
recoveryConfiguration.RecoveryCheckIntervalSeconds = 1800; // 30 minutes
```

### MaximumRetries
- **Type**: `int`
- **Default**: `3`
- **Description**: Maximum number of retries for recovered jobs before marking as failed
- **Impact**: Controls how many recovery attempts are made for stuck jobs

```csharp
// No retries for recovered jobs
recoveryConfiguration.MaximumRetries = 0;

// Standard retry count (default)
recoveryConfiguration.MaximumRetries = 3;

// More retries for critical operations
recoveryConfiguration.MaximumRetries = 5;
```

## Configuration Setup

### Basic Recovery Configuration

```csharp
builder.Services.AddAsyncEndpointsWorker(recoveryConfiguration =>
{
    recoveryConfiguration.EnableDistributedJobRecovery = true; // Default: true
    recoveryConfiguration.JobTimeoutMinutes = 30; // Default: 30 minutes
    recoveryConfiguration.RecoveryCheckIntervalSeconds = 300; // Default: 5 minutes
    recoveryConfiguration.MaximumRetries = 3; // Default: 3 retries
});
```

### Conservative Recovery Configuration

```csharp
builder.Services.AddAsyncEndpointsWorker(recoveryConfiguration =>
{
    recoveryConfiguration.EnableDistributedJobRecovery = true;
    recoveryConfiguration.JobTimeoutMinutes = 60; // Longer timeout to avoid false positives
    recoveryConfiguration.RecoveryCheckIntervalSeconds = 600; // Less frequent checks (10 minutes)
    recoveryConfiguration.MaximumRetries = 2; // Fewer retries to reduce load
});
```

### Aggressive Recovery Configuration

```csharp
builder.Services.AddAsyncEndpointsWorker(recoveryConfiguration =>
{
    recoveryConfiguration.EnableDistributedJobRecovery = true;
    recoveryConfiguration.JobTimeoutMinutes = 15; // Short timeout for fast recovery
    recoveryConfiguration.RecoveryCheckIntervalSeconds = 60; // Check every minute
    recoveryConfiguration.MaximumRetries = 5; // More retries for resilience
});
```

## Recovery Process Flow

The distributed recovery process works as follows:

1. **Detection Phase**: Every `RecoveryCheckIntervalSeconds`, the recovery service scans for jobs in `InProgress` status
2. **Timeout Check**: Jobs that have been in progress longer than `JobTimeoutMinutes` are identified
3. **Recovery Action**: Identified jobs are treated as failed and recovery logic is applied
4. **Retry Logic**: If retries are available, the job is reset for reprocessing; otherwise, marked as failed

```csharp
// Pseudo-code for recovery process
public async Task<int> PerformRecoveryAsync()
{
    var currentTime = DateTimeOffset.UtcNow;
    var timeoutThreshold = currentTime.AddMinutes(-JobTimeoutMinutes);
    
    // Find jobs in progress that exceeded timeout
    var stuckJobs = await GetJobsInProgressBefore(timeoutThreshold);
    
    int recoveredJobs = 0;
    foreach (var job in stuckJobs)
    {
        // Apply retry logic
        if (job.RetryCount < MaximumRetries)
        {
            job.IncrementRetryCount();
            job.WorkerId = null; // Release from current (failed) worker
            job.Status = JobStatus.Queued; // Make available for processing
            await UpdateJob(job);
            recoveredJobs++;
        }
        else
        {
            // Mark as permanently failed
            job.Status = JobStatus.Failed;
            job.Error = AsyncEndpointError.FromMessage("Job failed after recovery attempts");
            await UpdateJob(job);
        }
    }
    
    return recoveredJobs;
}
```

## Multi-Instance Considerations

### Recovery in Clustered Environments

In multi-instance deployments, recovery configuration should account for:

- **Consistent Timeout Values**: All instances should use the same timeout settings
- **Balanced Check Intervals**: Avoid having all instances check simultaneously to reduce resource contention
- **Appropriate Retry Counts**: Consider the total system capacity when setting retries

```csharp
// Configuration for clustered environment
builder.Services.AddAsyncEndpointsWorker(recoveryConfiguration =>
{
    recoveryConfiguration.EnableDistributedJobRecovery = true;
    
    // Set timeout based on longest expected job in the system
    recoveryConfiguration.JobTimeoutMinutes = CalculateMaxExpectedJobDuration();
    
    // Balance check frequency with instance count to avoid overwhelming storage
    recoveryConfiguration.RecoveryCheckIntervalSeconds = 300; // 5 minutes
    
    // Set retry count considering total cluster capacity
    recoveryConfiguration.MaximumRetries = 3;
});
```

### Instance-Specific Adjustments

You can adjust recovery settings per instance based on their role or capacity:

```csharp
var instanceName = Environment.GetEnvironmentVariable("INSTANCE_NAME") ?? "default";

builder.Services.AddAsyncEndpointsWorker(recoveryConfiguration =>
{
    recoveryConfiguration.EnableDistributedJobRecovery = true;
    
    switch (instanceName)
    {
        case "high-priority":
            recoveryConfiguration.JobTimeoutMinutes = 15; // Shorter timeout for critical instances
            recoveryConfiguration.RecoveryCheckIntervalSeconds = 120; // More frequent checks
            break;
        case "low-priority":
            recoveryConfiguration.JobTimeoutMinutes = 60; // Longer timeout
            recoveryConfiguration.RecoveryCheckIntervalSeconds = 600; // Less frequent checks
            break;
        default:
            // Standard configuration
            recoveryConfiguration.JobTimeoutMinutes = 30;
            recoveryConfiguration.RecoveryCheckIntervalSeconds = 300;
            break;
    }
    
    recoveryConfiguration.MaximumRetries = 3;
});
```

## Performance Considerations

### Resource Impact

Recovery operations have resource implications:

- **Storage Load**: Recovery checks involve database queries that can impact performance
- **Processing Overhead**: Identifying and updating stuck jobs requires processing time
- **Network Traffic**: In distributed storage scenarios, recovery adds network calls

### Optimizing Recovery Settings

Based on system performance:

```csharp
// For high-performance systems
if (systemPerformance > PerformanceThreshold.High)
{
    recoveryConfiguration.JobTimeoutMinutes = 45; // Longer to reduce false positives
    recoveryConfiguration.RecoveryCheckIntervalSeconds = 600; // Less frequent
}
else
{
    recoveryConfiguration.JobTimeoutMinutes = 20; // More aggressive recovery
    recoveryConfiguration.RecoveryCheckIntervalSeconds = 120; // More frequent checks
}
```

## Storage-Specific Recovery Behavior

### In-Memory Storage Recovery

Recovery behavior differs based on storage implementation:

```csharp
// In-memory storage has immediate access but no persistence across restarts
// Recovery is mainly useful for detecting truly stuck jobs during runtime
```

### Redis Storage Recovery

Redis storage provides more sophisticated recovery capabilities:

```csharp
// Redis supports atomic operations for recovery
// Lua scripts ensure consistency during recovery operations
// Sorted sets enable efficient timeout detection
```

## Monitoring Recovery Operations

### Recovery Metrics

You can monitor recovery operations with custom configuration:

```csharp
builder.Services.AddAsyncEndpointsWorker(recoveryConfiguration =>
{
    recoveryConfiguration.EnableDistributedJobRecovery = true;
    recoveryConfiguration.JobTimeoutMinutes = 30;
    recoveryConfiguration.RecoveryCheckIntervalSeconds = 300;
    recoveryConfiguration.MaximumRetries = 3;
});

// Add recovery monitoring service
builder.Services.AddSingleton<IHostedService, RecoveryMonitoringService>();
```

### Recovery Event Logging

Configure detailed logging for recovery events:

```csharp
// Custom recovery configuration with detailed logging
public class DetailedRecoveryConfiguration
{
    public bool EnableDistributedJobRecovery { get; set; } = true;
    public int JobTimeoutMinutes { get; set; } = 30;
    public int RecoveryCheckIntervalSeconds { get; set; } = 300;
    public int MaximumRetries { get; set; } = 3;
    
    public Action<string, object[]> LogRecoveryAction { get; set; } = (message, args) => 
        Console.WriteLine($"[RECOVERY] {message}", args);
}
```

## Error Handling in Recovery

### Recovery Failure Scenarios

Recovery itself can fail, so configure appropriately:

```csharp
builder.Services.AddAsyncEndpointsWorker(recoveryConfiguration =>
{
    recoveryConfiguration.EnableDistributedJobRecovery = true;
    
    // Set realistic timeouts to avoid recovery process hanging
    recoveryConfiguration.JobTimeoutMinutes = Math.Max(10, GetMaxExpectedJobDuration());
    
    // Limit retries to prevent infinite loops
    recoveryConfiguration.MaximumRetries = Math.Min(10, StandardRetryCount);
    
    // Balance check frequency to detect issues without overwhelming system
    recoveryConfiguration.RecoveryCheckIntervalSeconds = Math.Max(60, StandardCheckInterval);
});
```

## Configuration Best Practices

### Production-Ready Configuration

```csharp
builder.Services.AddAsyncEndpointsWorker(recoveryConfiguration =>
{
    // Always enable in production
    recoveryConfiguration.EnableDistributedJobRecovery = true;
    
    // Set timeout based on your longest expected job duration
    // Add buffer time to avoid false positives
    recoveryConfiguration.JobTimeoutMinutes = 60; // Adjust based on your use case
    
    // Balance between fast detection and resource usage
    recoveryConfiguration.RecoveryCheckIntervalSeconds = 300; // 5 minutes
    
    // Allow sufficient retries for resilience
    recoveryConfiguration.MaximumRetries = 3;
});
```

### Development vs Production

```csharp
if (builder.Environment.IsDevelopment())
{
    recoveryConfiguration.EnableDistributedJobRecovery = true; // Still useful for testing
    recoveryConfiguration.JobTimeoutMinutes = 10; // Faster timeouts for development
    recoveryConfiguration.RecoveryCheckIntervalSeconds = 60; // More frequent checks
    recoveryConfiguration.MaximumRetries = 1; // Fewer retries in development
}
else
{
    recoveryConfiguration.EnableDistributedJobRecovery = true;
    recoveryConfiguration.JobTimeoutMinutes = 60; // Production-appropriate timeout
    recoveryConfiguration.RecoveryCheckIntervalSeconds = 300; // Balanced checking
    recoveryConfiguration.MaximumRetries = 3; // Production-appropriate retries
}
```

## Troubleshooting Recovery Issues

### Common Issues and Solutions

#### Too Frequent Recovery Actions
- **Issue**: Recovery runs too often, consuming resources
- **Solution**: Increase `RecoveryCheckIntervalSeconds`

#### Jobs Marked as Stuck Prematurely
- **Issue**: Jobs are recovered before they can complete legitimately
- **Solution**: Increase `JobTimeoutMinutes`

#### Recovery Not Detecting Stuck Jobs
- **Issue**: Stuck jobs remain unprocessed
- **Solution**: Decrease `RecoveryCheckIntervalSeconds` or decrease `JobTimeoutMinutes`

#### Recovery Overload
- **Issue**: Recovery process affects system performance
- **Solution**: Increase check intervals and optimize timeout settings

### Recovery Validation

Test recovery configuration:

```csharp
// In integration tests, verify recovery works
public async Task ValidateRecoveryConfiguration()
{
    // Submit a job that hangs indefinitely
    // Wait for timeout period
    // Verify job is recovered
    // Check retry count and status
}
```

Distributed recovery configuration is essential for maintaining system reliability in multi-instance deployments, ensuring that job processing continues smoothly even when individual workers experience failures.