---
sidebar_position: 4
title: Job Manager Configuration
---

# Job Manager Configuration

This page details the configuration options for the AsyncEndpoints Job Manager, which handles job lifecycle management, retry logic, and claim operations.

## Overview

The Job Manager configuration controls how jobs are managed throughout their lifecycle, including retry mechanisms, timeout handling, and job claiming behavior. These settings are crucial for reliability and performance.

## Job Manager Configuration Properties

### DefaultMaxRetries
- **Type**: `int`
- **Default**: `3`
- **Description**: Maximum number of retries for failed jobs before marking as failed permanently
- **Impact**: Affects system resilience and job completion rates

```csharp
// No retries - fail immediately
options.JobManagerConfiguration.DefaultMaxRetries = 0;

// Standard 3 retries (default)
options.JobManagerConfiguration.DefaultMaxRetries = 3;

// More retries for critical operations
options.JobManagerConfiguration.DefaultMaxRetries = 10;
```

### RetryDelayBaseSeconds
- **Type**: `double`
- **Default**: `2.0`
- **Description**: Base delay in seconds for exponential backoff retry mechanism
- **Impact**: Affects retry timing and system load during retries

The retry delay follows an exponential backoff pattern: `(2 ^ retryCount) * baseDelay`

```csharp
// Fast retries for transient issues
options.JobManagerConfiguration.RetryDelayBaseSeconds = 0.5; // 0.5s, 1s, 2s, 4s...

// Standard retry timing (default)
options.JobManagerConfiguration.RetryDelayBaseSeconds = 2.0; // 2s, 4s, 8s, 16s...

// Slow retries for expensive operations
options.JobManagerConfiguration.RetryDelayBaseSeconds = 10.0; // 10s, 20s, 40s, 80s...
```

### JobClaimTimeout
- **Type**: `TimeSpan`
- **Default**: `TimeSpan.FromMinutes(5)`
- **Description**: Timeout for job claim operations to prevent indefinite claiming
- **Impact**: Affects distributed job recovery timing

```csharp
// Short timeout for fast recovery
options.JobManagerConfiguration.JobClaimTimeout = TimeSpan.FromMinutes(1);

// Standard timeout (default)
options.JobManagerConfiguration.JobClaimTimeout = TimeSpan.FromMinutes(5);

// Long timeout for complex operations
options.JobManagerConfiguration.JobClaimTimeout = TimeSpan.FromMinutes(15);
```

### MaxConcurrentJobs
- **Type**: `int`
- **Default**: `10`
- **Description**: Maximum number of concurrent jobs that can be processed
- **Impact**: Controls resource usage and system load

```csharp
// Conservative processing
options.JobManagerConfiguration.MaxConcurrentJobs = 5;

// Standard processing (default)
options.JobManagerConfiguration.MaxConcurrentJobs = 10;

// High throughput processing
options.JobManagerConfiguration.MaxConcurrentJobs = 50;
```

### JobPollingIntervalMs
- **Type**: `int`
- **Default**: `1000` (1 second)
- **Description**: Polling interval in milliseconds for job status checks
- **Impact**: Affects responsiveness vs. system load

```csharp
// Fast polling for responsive systems
options.JobManagerConfiguration.JobPollingIntervalMs = 100; // 100ms

// Standard polling (default)
options.JobManagerConfiguration.JobPollingIntervalMs = 1000; // 1s

// Conservative polling to reduce load
options.JobManagerConfiguration.JobPollingIntervalMs = 5000; // 5s
```

### MaxClaimBatchSize
- **Type**: `int`
- **Default**: `10`
- **Description**: Maximum number of jobs to claim in a single batch operation
- **Impact**: Affects batch processing efficiency

```csharp
// Small batches for responsiveness
options.JobManagerConfiguration.MaxClaimBatchSize = 1;

// Standard batch size (default)
options.JobManagerConfiguration.MaxClaimBatchSize = 10;

// Large batches for throughput
options.JobManagerConfiguration.MaxClaimBatchSize = 50;
```

### StaleJobClaimCheckInterval
- **Type**: `TimeSpan`
- **Default**: `TimeSpan.FromMinutes(1)`
- **Description**: Interval for checking for stale job claims during distributed recovery
- **Impact**: Affects recovery frequency for stuck jobs

```csharp
// Frequent checks for fast recovery
options.JobManagerConfiguration.StaleJobClaimCheckInterval = TimeSpan.FromSeconds(30);

// Standard check interval (default)
options.JobManagerConfiguration.StaleJobClaimCheckInterval = TimeSpan.FromMinutes(1);

// Less frequent checks to reduce load
options.JobManagerConfiguration.StaleJobClaimCheckInterval = TimeSpan.FromMinutes(5);
```

## Retry Configuration Examples

### Fast Retry Strategy
For operations that fail due to transient issues:

```csharp
options.JobManagerConfiguration.DefaultMaxRetries = 5;
options.JobManagerConfiguration.RetryDelayBaseSeconds = 1.0; // Quick retries: 1s, 2s, 4s, 8s, 16s
```

### Conservative Retry Strategy
For operations where failures might be persistent:

```csharp
options.JobManagerConfiguration.DefaultMaxRetries = 2;
options.JobManagerConfiguration.RetryDelayBaseSeconds = 5.0; // Slower retries: 5s, 10s, 20s
```

### Aggressive Retry Strategy
For critical operations that must succeed:

```csharp
options.JobManagerConfiguration.DefaultMaxRetries = 10;
options.JobManagerConfiguration.RetryDelayBaseSeconds = 2.0; // Standard timing with many retries
```

## Configuration for Different Scenarios

### Development Configuration
```csharp
options.JobManagerConfiguration.DefaultMaxRetries = 1; // Fewer retries for faster debugging
options.JobManagerConfiguration.RetryDelayBaseSeconds = 1.0; // Faster retries during development
options.JobManagerConfiguration.JobClaimTimeout = TimeSpan.FromMinutes(1); // Faster timeouts
options.JobManagerConfiguration.MaxConcurrentJobs = 5; // Lower limits for development
options.JobManagerConfiguration.JobPollingIntervalMs = 500; // More frequent polling
options.JobManagerConfiguration.MaxClaimBatchSize = 5; // Smaller batches
options.JobManagerConfiguration.StaleJobClaimCheckInterval = TimeSpan.FromMinutes(1); // Standard checks
```

### Production Configuration for Critical Operations
```csharp
options.JobManagerConfiguration.DefaultMaxRetries = 5; // More retries for resilience
options.JobManagerConfiguration.RetryDelayBaseSeconds = 2.0; // Standard exponential backoff
options.JobManagerConfiguration.JobClaimTimeout = TimeSpan.FromMinutes(10); // Longer for complex ops
options.JobManagerConfiguration.MaxConcurrentJobs = 50; // Higher concurrency for throughput
options.JobManagerConfiguration.JobPollingIntervalMs = 2000; // Balanced polling
options.JobManagerConfiguration.MaxClaimBatchSize = 20; // Larger batches for efficiency
options.JobManagerConfiguration.StaleJobClaimCheckInterval = TimeSpan.FromMinutes(2); // Regular checks
```

### High-Throughput Configuration
```csharp
options.JobManagerConfiguration.DefaultMaxRetries = 3; // Standard retries
options.JobManagerConfiguration.RetryDelayBaseSeconds = 1.5; // Faster initial retries
options.JobManagerConfiguration.JobClaimTimeout = TimeSpan.FromMinutes(5); // Standard timeout
options.JobManagerConfiguration.MaxConcurrentJobs = 100; // High concurrency
options.JobManagerConfiguration.JobPollingIntervalMs = 500; // Fast polling
options.JobManagerConfiguration.MaxClaimBatchSize = 50; // Large batches
options.JobManagerConfiguration.StaleJobClaimCheckInterval = TimeSpan.FromMinutes(1); // Regular checks
```

## Performance Considerations

### Retry Settings Impact
- **Higher `DefaultMaxRetries`**: Better success rate but longer processing time
- **Lower `RetryDelayBaseSeconds`**: Faster retries but more system load during failures
- **Higher `RetryDelayBaseSeconds`**: Less load during failures but longer total processing time

### Concurrency Settings Impact
- **Higher `MaxConcurrentJobs`**: Better throughput but higher resource usage
- **Lower `MaxConcurrentJobs`**: Lower resource usage but potential queue buildup

### Polling Settings Impact
- **Higher `JobPollingIntervalMs`**: Less system load but less responsive
- **Lower `JobPollingIntervalMs`**: More responsive but higher system load

## Distributed System Considerations

For multi-instance deployments:

```csharp
// In larger clusters, you might reduce some settings to avoid contention
options.JobManagerConfiguration.MaxClaimBatchSize = 5; // Smaller batches reduce contention
options.JobManagerConfiguration.JobPollingIntervalMs = 2000; // Reduce check frequency
options.JobManagerConfiguration.StaleJobClaimCheckInterval = TimeSpan.FromMinutes(2); // Balance recovery time
```

## Monitoring and Adjustment

Configure based on observed behavior:

```csharp
// Example: Adjust based on failure patterns
if (failureRate > 0.1) // If more than 10% of jobs fail
{
    options.JobManagerConfiguration.DefaultMaxRetries = 5; // Increase retries
    options.JobManagerConfiguration.RetryDelayBaseSeconds = 3.0; // Slower retries
}
else
{
    options.JobManagerConfiguration.DefaultMaxRetries = 2; // Reduce retries for healthy system
    options.JobManagerConfiguration.RetryDelayBaseSeconds = 1.0; // Faster retries
}
```

## Error Handling Configuration

The job manager configuration also impacts error handling patterns:

```csharp
// For operations where some failures are acceptable
options.JobManagerConfiguration.DefaultMaxRetries = 1;
options.JobManagerConfiguration.RetryDelayBaseSeconds = 1.0;

// For operations where success is critical
options.JobManagerConfiguration.DefaultMaxRetries = 7;
options.JobManagerConfiguration.RetryDelayBaseSeconds = 2.0;
```

## Integration with Worker Configuration

Job manager settings should align with worker configuration:

```csharp
options.WorkerConfigurations.MaximumConcurrency = 10;
options.JobManagerConfiguration.MaxConcurrentJobs = 15; // Slightly higher to allow for queuing

options.WorkerConfigurations.JobTimeoutMinutes = 30;
options.JobManagerConfiguration.JobClaimTimeout = TimeSpan.FromMinutes(15); // Shorter than job timeout
```

## Troubleshooting Common Issues

### Too Many Retries
- Reduce `DefaultMaxRetries` if failures are persistent
- Increase `RetryDelayBaseSeconds` to reduce system load during retries

### Slow Recovery from Failures
- Decrease `RetryDelayBaseSeconds` for faster initial retries
- Increase `StaleJobClaimCheckInterval` if recovery is too aggressive

### Resource Exhaustion
- Decrease `MaxConcurrentJobs` to limit concurrent processing
- Increase `JobPollingIntervalMs` to reduce system load

### Job Stuck in Processing
- Verify `JobClaimTimeout` is appropriate for your processing times
- Adjust `StaleJobClaimCheckInterval` for faster detection of stuck jobs

Proper job manager configuration is essential for achieving the right balance between system resilience, performance, and resource utilization.