---
sidebar_position: 1
title: Core Configuration
---

# Core Configuration

This page explains how to configure the AsyncEndpoints library using the comprehensive configuration system that allows fine-tuning of all aspects of the async processing pipeline.

## Overview

The AsyncEndpoints configuration system is built around the `AsyncEndpointsConfigurations` class, which provides access to three main configuration sections:

- **Worker Configurations**: Settings for background job processing
- **Job Manager Configurations**: Settings for job lifecycle management
- **Response Configurations**: Settings for HTTP response customization

## Basic Configuration Setup

### Using Default Configuration

The simplest way to configure AsyncEndpoints is to use the default settings:

```csharp
using AsyncEndpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAsyncEndpoints() // Uses default configuration
    .AddAsyncEndpointsInMemoryStore()
    .AddAsyncEndpointsWorker();

var app = builder.Build();
```

### Custom Configuration with Fluent API

You can customize all configuration aspects using the fluent API:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    // Configure worker settings
    options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount;
    options.WorkerConfigurations.PollingIntervalMs = 1000;
    options.WorkerConfigurations.JobTimeoutMinutes = 30;
    
    // Configure job manager settings
    options.JobManagerConfiguration.DefaultMaxRetries = 3;
    options.JobManagerConfiguration.RetryDelayBaseSeconds = 2.0;
    
    // Configure response customization
    options.ResponseConfigurations.JobSubmittedResponseFactory = async (job, context) =>
    {
        context.Response.Headers.Append("Async-Job-Id", job.Id.ToString());
        return Results.Accepted($"/jobs/{job.Id}", job);
    };
});
```

## Configuration Classes Hierarchy

### AsyncEndpointsConfigurations

This is the main configuration class that contains all sub-configurations:

```csharp
public sealed class AsyncEndpointsConfigurations
{
    public AsyncEndpointsWorkerConfigurations WorkerConfigurations { get; set; } = new();
    public AsyncEndpointsJobManagerConfiguration JobManagerConfiguration { get; set; } = new();
    public AsyncEndpointsResponseConfigurations ResponseConfigurations { get; set; } = new();
}
```

### AsyncEndpointsWorkerConfigurations

Configuration for background worker services:

```csharp
public sealed class AsyncEndpointsWorkerConfigurations
{
    public Guid WorkerId { get; set; } = Guid.NewGuid();
    public int MaximumConcurrency { get; set; } = Environment.ProcessorCount;
    public int PollingIntervalMs { get; set; } = AsyncEndpointsConstants.DefaultPollingIntervalMs; // 1000 ms
    public int JobTimeoutMinutes { get; set; } = AsyncEndpointsConstants.DefaultJobTimeoutMinutes; // 30 minutes
    public int BatchSize { get; set; } = AsyncEndpointsConstants.DefaultBatchSize; // 5
    public int MaximumQueueSize { get; set; } = AsyncEndpointsConstants.DefaultMaximumQueueSize; // 50
}
```

### AsyncEndpointsJobManagerConfiguration

Configuration for job management and retry logic:

```csharp
public sealed class AsyncEndpointsJobManagerConfiguration
{
    public int DefaultMaxRetries { get; set; } = AsyncEndpointsConstants.MaximumRetries; // 3
    public double RetryDelayBaseSeconds { get; set; } = 2.0;
    public TimeSpan JobClaimTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxConcurrentJobs { get; set; } = 10;
    public int JobPollingIntervalMs { get; set; } = 1000;
    public int MaxClaimBatchSize { get; set; } = 10;
    public TimeSpan StaleJobClaimCheckInterval { get; set; } = TimeSpan.FromMinutes(1);
}
```

### AsyncEndpointsResponseConfigurations

Configuration for response customization:

```csharp
public sealed class AsyncEndpointsResponseConfigurations
{
    public Func<Job, HttpContext, Task<IResult>> JobSubmittedResponseFactory { get; set; } = ResponseDefaults.CreateJobSubmittedResponse;
    public Func<MethodResult<Job>, HttpContext, Task<IResult>> JobStatusResponseFactory { get; set; } = ResponseDefaults.CreateJobStatusResponse;
    public Func<AsyncEndpointError?, HttpContext, Task<IResult>> JobSubmissionErrorResponseFactory { get; set; } = ResponseDefaults.CreateJobSubmissionErrorResponse;
    public Func<Exception, HttpContext, Task<IResult>> ExceptionResponseFactory { get; set; } = ResponseDefaults.CreateExceptionResponse;
}
```

## Configuration Validation

The library does not include built-in validation for configuration values. The configuration system accepts all values as provided, so it's important to validate configuration values manually during setup:

```csharp
// Example validation approach
public void ValidateConfiguration(AsyncEndpointsConfigurations config)
{
    if (config.WorkerConfigurations.MaximumConcurrency <= 0)
        throw new ArgumentException("MaximumConcurrency must be greater than 0");
    
    if (config.WorkerConfigurations.MaximumQueueSize <= 0)
        throw new ArgumentException("MaximumQueueSize must be greater than 0");
    
    if (config.JobManagerConfiguration.DefaultMaxRetries < 0)
        throw new ArgumentException("DefaultMaxRetries cannot be negative");
    
    if (config.WorkerConfigurations.JobTimeoutMinutes <= 0)
        throw new ArgumentException("JobTimeoutMinutes must be greater than 0");
}

// Call validation after configuration
builder.Services.AddAsyncEndpoints(options =>
{
    // Configure options
    options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount;
    // ... other configuration
    
    // Validate the configuration
    ValidateConfiguration(options);
});
```

## Common Configuration Patterns

### Development Configuration

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    // More verbose settings for development
    options.WorkerConfigurations.MaximumConcurrency = 2; // Lower concurrency for debugging
    options.WorkerConfigurations.PollingIntervalMs = 1000; // Faster polling
    options.WorkerConfigurations.JobTimeoutMinutes = 10; // Shorter timeouts
    options.WorkerConfigurations.MaximumQueueSize = 10; // Smaller queue for testing
    
    options.JobManagerConfiguration.DefaultMaxRetries = 1; // Fewer retries in development
    options.JobManagerConfiguration.RetryDelayBaseSeconds = 1.0; // Faster retries
});
```

### Production Configuration

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    // Optimized settings for production
    options.WorkerConfigurations.MaximumConcurrency = Math.Min(Environment.ProcessorCount, 16);
    options.WorkerConfigurations.PollingIntervalMs = 3000; // Balance between responsiveness and resource usage (default is 1000ms)
    options.WorkerConfigurations.JobTimeoutMinutes = 60; // Longer timeouts for complex operations
    options.WorkerConfigurations.MaximumQueueSize = 1000; // Larger queue for high throughput
    
    options.JobManagerConfiguration.DefaultMaxRetries = 5; // More retries for reliability
    options.JobManagerConfiguration.RetryDelayBaseSeconds = 2.0; // Standard exponential backoff
    
    // Custom response factory for monitoring
    options.ResponseConfigurations.JobSubmittedResponseFactory = async (job, context) =>
    {
        // Add custom headers for monitoring
        context.Response.Headers.Append("X-Async-Job-Id", job.Id.ToString());
        context.Response.Headers.Append("X-Async-Job-Name", job.Name);
        
        return Results.Accepted($"/jobs/{job.Id}", job);
    };
});
```

## Environment-Based Configuration

You can configure different settings based on the hosting environment:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.WorkerConfigurations.MaximumConcurrency = 2;
        options.WorkerConfigurations.MaximumQueueSize = 10;
        options.JobManagerConfiguration.DefaultMaxRetries = 1;
    }
    else if (builder.Environment.IsProduction())
    {
        options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount;
        options.WorkerConfigurations.MaximumQueueSize = 500;
        options.JobManagerConfiguration.DefaultMaxRetries = 3;
    }
    
    // Common settings for all environments
    options.WorkerConfigurations.PollingIntervalMs = 2000;
});
```

## Configuration Best Practices

### Use Appropriate Concurrency Levels

- Start with `Environment.ProcessorCount` for CPU-bound operations
- Adjust based on I/O patterns (may be higher for I/O-bound operations)
- Monitor system performance to optimize

### Set Realistic Timeouts

- Job timeouts should be based on expected processing times
- Consider external dependencies when setting timeouts
- Account for retry delays in overall timeout calculations

### Configure Queue Size Appropriately

- Queue size limits provide circuit breaker functionality
- Balance between throughput and memory usage
- Consider the number of concurrent workers when setting limits

### Validate Configuration Changes

- Test configuration changes in a staging environment
- Monitor performance metrics after changes
- Have rollback plans for configuration changes

The core configuration system provides the flexibility to optimize AsyncEndpoints for your specific use cases while maintaining system reliability and performance.