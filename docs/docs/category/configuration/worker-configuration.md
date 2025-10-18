---
sidebar_position: 3
title: Worker Configuration
---

# Worker Configuration

This page details the configuration options for AsyncEndpoints background workers, including concurrency settings, polling intervals, and queue management.

## Overview

Worker configuration controls how background services process async jobs. These settings determine performance characteristics, resource usage, and system responsiveness.

## Worker Configuration Properties

### WorkerId
- **Type**: `Guid`
- **Default**: `Guid.NewGuid()`
- **Description**: Unique identifier for the worker instance in multi-instance deployments
- **Usage**: Helps identify which worker processes each job

```csharp
// Using default GUID
options.WorkerConfigurations.WorkerId = Guid.NewGuid();

// Using custom worker ID
options.WorkerConfigurations.WorkerId = new Guid("12345678-1234-1234-1234-123456789012");
```

### MaximumConcurrency
- **Type**: `int`
- **Default**: `Environment.ProcessorCount`
- **Description**: Maximum number of concurrent jobs the worker can process
- **Impact**: Controls resource usage and system load

```csharp
// Use all available processors
options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount;

// Limit to 4 concurrent jobs
options.WorkerConfigurations.MaximumConcurrency = 4;

// Use half the available processors
options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount / 2;
```

### PollingIntervalMs
- **Type**: `int`
- **Default**: `5000` (5 seconds)
- **Description**: Frequency in milliseconds for checking new jobs
- **Impact**: Affects responsiveness vs. resource consumption

```csharp
// Check for jobs every 1 second (more responsive but more frequent checks)
options.WorkerConfigurations.PollingIntervalMs = 1000;

// Check for jobs every 10 seconds (less responsive but fewer checks)
options.WorkerConfigurations.PollingIntervalMs = 10000;
```

### JobTimeoutMinutes
- **Type**: `int`
- **Default**: `30` (30 minutes)
- **Description**: Timeout in minutes for individual job execution
- **Impact**: Prevents jobs from running indefinitely

```csharp
// 10-minute timeout for fast operations
options.WorkerConfigurations.JobTimeoutMinutes = 10;

// 2-hour timeout for long-running operations
options.WorkerConfigurations.JobTimeoutMinutes = 120;
```

### BatchSize
- **Type**: `int`
- **Default**: `5`
- **Description**: Maximum number of jobs to process in a single batch
- **Impact**: Affects batch processing efficiency

```csharp
// Process 10 jobs per batch (higher throughput but bigger batches)
options.WorkerConfigurations.BatchSize = 10;

// Process 1 job at a time (more responsive but lower throughput)
options.WorkerConfigurations.BatchSize = 1;
```

### MaximumQueueSize
- **Type**: `int`
- **Default**: `50`
- **Description**: Maximum size of the job queue before new jobs are rejected
- **Impact**: Provides circuit breaker functionality

```csharp
// Small queue for development/testing
options.WorkerConfigurations.MaximumQueueSize = 10;

// Large queue for high-throughput production
options.WorkerConfigurations.MaximumQueueSize = 1000;
```

## Performance Tuning Guidelines

### For CPU-Bound Operations
```csharp
options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount;
options.WorkerConfigurations.PollingIntervalMs = 2000; // Moderate frequency
options.WorkerConfigurations.BatchSize = 1; // Process individually for better load distribution
```

### For I/O-Bound Operations
```csharp
options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount * 2; // Higher concurrency for I/O
options.WorkerConfigurations.PollingIntervalMs = 1000; // More frequent checks due to I/O nature
options.WorkerConfigurations.BatchSize = 5; // Reasonable batch size
```

### For Mixed Workloads
```csharp
options.WorkerConfigurations.MaximumConcurrency = Math.Min(Environment.ProcessorCount * 2, 16);
options.WorkerConfigurations.PollingIntervalMs = 3000; // Balanced frequency
options.WorkerConfigurations.BatchSize = 3; // Small batches for responsiveness
options.WorkerConfigurations.MaximumQueueSize = 200; // Reasonable queue size
```

## Circuit Breaker Configuration

The queue size limits provide circuit breaker functionality:

```csharp
// Configure based on expected load and memory constraints
options.WorkerConfigurations.MaximumQueueSize = expectedJobsPerMinute * 5; // 5 minutes worth of jobs

// Set appropriate timeout based on expected job duration
options.WorkerConfigurations.JobTimeoutMinutes = maxExpectedJobDurationInMinutes * 2;
```

## Multi-Instance Considerations

When running multiple instances, consider the total system capacity:

```csharp
// If running 3 instances, configure each with appropriate limits
var totalConcurrency = 24; // Total desired concurrency across all instances
var instanceCount = 3;
var concurrencyPerInstance = totalConcurrency / instanceCount; // 8

options.WorkerConfigurations.MaximumConcurrency = concurrencyPerInstance;
```

## Monitoring and Metrics

Configure settings based on observed metrics:

```csharp
// Adjust based on historical data
if (averageJobDuration > TimeSpan.FromMinutes(1))
{
    // Increase timeout for longer jobs
    options.WorkerConfigurations.JobTimeoutMinutes = 10;
    
    // Reduce concurrency to prevent resource exhaustion
    options.WorkerConfigurations.MaximumConcurrency = Math.Max(1, Environment.ProcessorCount / 2);
}
```

## Example Production Configuration

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    // Production-optimized worker configuration
    options.WorkerConfigurations.WorkerId = Guid.NewGuid(); // Unique per instance
    
    // Concurrency based on load and resource availability
    options.WorkerConfigurations.MaximumConcurrency = Math.Min(Environment.ProcessorCount, 16);
    
    // Balanced polling frequency
    options.WorkerConfigurations.PollingIntervalMs = 5000;
    
    // Reasonable timeout for most operations
    options.WorkerConfigurations.JobTimeoutMinutes = 60;
    
    // Efficient batch processing
    options.WorkerConfigurations.BatchSize = 5;
    
    // Circuit breaker protection
    options.WorkerConfigurations.MaximumQueueSize = 1000;
});
```

## Example Development Configuration

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        // Lower concurrency for debugging
        options.WorkerConfigurations.MaximumConcurrency = 2;
        
        // Faster polling for better development experience
        options.WorkerConfigurations.PollingIntervalMs = 1000;
        
        // Shorter timeouts for faster feedback
        options.WorkerConfigurations.JobTimeoutMinutes = 10;
        
        // Smaller batch size for easier debugging
        options.WorkerConfigurations.BatchSize = 1;
        
        // Smaller queue for development
        options.WorkerConfigurations.MaximumQueueSize = 20;
    }
});
```

## Performance Impact Analysis

### Concurrency Impact
- **Higher values**: More parallel processing but higher resource usage
- **Lower values**: Less resource usage but potential queue buildup

### Polling Frequency Impact
- **Higher frequency**: More responsive but more system calls
- **Lower frequency**: Less responsive but more efficient

### Queue Size Impact
- **Larger queues**: Can handle traffic spikes but use more memory
- **Smaller queues**: More memory efficient but may reject jobs under load

### Timeout Impact
- **Longer timeouts**: Accommodates long operations but may hold resources
- **Shorter timeouts**: Faster resource release but may cancel legitimate work

## Troubleshooting Common Issues

### High Resource Usage
- Reduce `MaximumConcurrency` to limit parallel processing
- Increase `PollingIntervalMs` to reduce system calls

### Job Backlog
- Increase `MaximumConcurrency` to process more jobs in parallel
- Increase `MaximumQueueSize` to accommodate more pending jobs
- Review job processing time and optimize handlers

### Slow Response Times
- Increase `MaximumConcurrency` to process more jobs simultaneously
- Decrease `PollingIntervalMs` to check for jobs more frequently
- Optimize job processing logic

Worker configuration is critical for achieving optimal performance and resource usage in your AsyncEndpoints application.