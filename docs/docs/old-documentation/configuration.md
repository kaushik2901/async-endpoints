---
sidebar_position: 5
---

# Configuration

## Core Settings

Configure AsyncEndpoints using the fluent API in your `Program.cs`:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    // Worker configuration (background processing)
    options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount;
    options.WorkerConfigurations.PollingIntervalMs = 1000;
    options.WorkerConfigurations.JobTimeoutMinutes = 30;
    options.WorkerConfigurations.BatchSize = 5;
    options.WorkerConfigurations.MaximumQueueSize = 50;
    
    // Job manager configuration (retry and processing logic)
    options.JobManagerConfiguration.DefaultMaxRetries = 3;
    options.JobManagerConfiguration.RetryDelayBaseSeconds = 2.0;
    options.JobManagerConfiguration.MaxConcurrentJobs = 10;
    options.JobManagerConfiguration.JobPollingIntervalMs = 1000;
    options.JobManagerConfiguration.MaxClaimBatchSize = 10;
    options.JobManagerConfiguration.JobClaimTimeout = TimeSpan.FromMinutes(5);
    options.JobManagerConfiguration.StaleJobClaimCheckInterval = TimeSpan.FromMinutes(1);
});
```

## Worker Configuration

Configure the background worker behavior:

| Setting | Default | Description |
|---------|---------|-------------|
| `WorkerId` | New GUID | Unique identifier for each worker instance |
| `MaximumConcurrency` | Processor count | Max concurrent jobs per worker |
| `PollingIntervalMs` | 1000 | How often the worker checks for new jobs (ms) |
| `JobTimeoutMinutes` | 30 | Time before a job is considered stuck (minutes) |
| `BatchSize` | 5 | Number of jobs to process in a single batch |
| `MaximumQueueSize` | 50 | Max jobs allowed in the queue |

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.WorkerConfigurations.MaximumConcurrency = 8; // Limit to 8 concurrent jobs
    options.WorkerConfigurations.PollingIntervalMs = 500; // Check every 500ms
    options.WorkerConfigurations.JobTimeoutMinutes = 15;  // 15 minute timeout
});
```

## Job Manager Configuration

Configure job processing and retry behavior:

| Setting | Default | Description |
|---------|---------|-------------|
| `DefaultMaxRetries` | 3 | Number of retry attempts for failed jobs |
| `RetryDelayBaseSeconds` | 2.0 | Base for exponential backoff (seconds) |
| `JobClaimTimeout` | 5 minutes | Time before job claim expires |
| `MaxConcurrentJobs` | 10 | Max jobs processed simultaneously |
| `JobPollingIntervalMs` | 1000 | How often to poll for jobs (ms) |
| `MaxClaimBatchSize` | 10 | Max jobs to claim in one batch |
| `StaleJobClaimCheckInterval` | 1 minute | How often to check for stuck jobs |

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.JobManagerConfiguration.DefaultMaxRetries = 5;              // 5 retry attempts
    options.JobManagerConfiguration.RetryDelayBaseSeconds = 3.0;        // 3s base delay
    options.JobManagerConfiguration.MaxConcurrentJobs = 20;             // 20 concurrent jobs
    options.JobManagerConfiguration.JobClaimTimeout = TimeSpan.FromMinutes(10); // 10 min claim timeout
});
```

## Distributed Recovery Configuration

Enable automatic recovery for stuck jobs in distributed environments:

```csharp
builder.Services.AddAsyncEndpointsWorker(recoveryConfiguration =>
{
    recoveryConfiguration.EnableDistributedJobRecovery = true;  // Default: true
    recoveryConfiguration.JobTimeoutMinutes = 30;               // Default: 30 minutes
    recoveryConfiguration.RecoveryCheckIntervalSeconds = 300;   // Default: 5 minutes
    recoveryConfiguration.MaximumRetries = 3;                   // Default: 3 retries
});
```

## Response Customization

Customize response formatting for different scenarios:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.ResponseConfigurations.JobSubmittedResponseFactory = async (job, context) =>
    {
        context.Response.Headers.Append("Async-Job-Id", job.Id.ToString());
        return Results.Accepted($"/jobs/{job.Id}", job);
    };
    
    options.ResponseConfigurations.JobStatusResponseFactory = async (jobResult, context) =>
    {
        if (jobResult.IsSuccess && jobResult.Data != null)
        {
            return Results.Ok(jobResult.Data);
        }
        return Results.Problem("Job not found", statusCode: 404);
    };
    
    options.ResponseConfigurations.ExceptionResponseFactory = async (exception, context) =>
    {
        return Results.Problem(
            title: "An error occurred",
            detail: exception.Message,
            statusCode: 500
        );
    };
});
```

## Storage Configuration

### In-Memory Store

```csharp
builder.Services.AddAsyncEndpointsInMemoryStore();
```

### Redis Store

#### Using Connection String
```csharp
builder.Services.AddAsyncEndpointsRedisStore("localhost:6379");
```

#### Using Connection Multiplexer
```csharp
var connection = ConnectionMultiplexer.Connect("localhost:6379");
builder.Services.AddAsyncEndpointsRedisStore(connection);
```

#### Using Configuration Object
```csharp
builder.Services.AddAsyncEndpointsRedisStore(config =>
{
    config.ConnectionString = "localhost:6379";
    // Additional configuration options can be set here
});
```

## Complete Example

Here's a complete configuration example:

```csharp
using AsyncEndpoints.Extensions;
using AsyncEndpoints.Redis.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Get Redis connection string from configuration
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";

builder.Services
    .AddAsyncEndpoints(options =>
    {
        // Worker configuration
        options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount;
        options.WorkerConfigurations.PollingIntervalMs = 1000;
        options.WorkerConfigurations.JobTimeoutMinutes = 30;

        // Job manager configuration
        options.JobManagerConfiguration.DefaultMaxRetries = 3;
        options.JobManagerConfiguration.RetryDelayBaseSeconds = 2.0;
        options.JobManagerConfiguration.MaxConcurrentJobs = 10;

        // Response customization
        options.ResponseConfigurations.JobSubmittedResponseFactory = async (job, context) =>
        {
            context.Response.Headers.Append("Async-Job-Id", job.Id.ToString());
            return Results.Accepted($"/jobs/{job.Id}", job);
        };
    })
    .AddAsyncEndpointsRedisStore(redisConnectionString)  // Production storage
    .AddAsyncEndpointsWorker(recoveryConfiguration =>
    {
        recoveryConfiguration.EnableDistributedJobRecovery = true;
        recoveryConfiguration.JobTimeoutMinutes = 30;
    });

var app = builder.Build();

app.MapAsyncGetJobDetails("/jobs/{jobId:guid}");

app.Run();
```

## Configuration Best Practices

1. **Performance**: Set `MaximumConcurrency` based on your system's capabilities
2. **Resource Management**: Configure appropriate `MaximumQueueSize` to prevent memory issues
3. **Job Timeouts**: Set job timeouts based on expected processing times
4. **Retry Configuration**: Adjust retry settings based on the nature of your operations
5. **Production**: Use Redis storage with proper connection configuration
6. **Monitoring**: Enable detailed logging to track job processing and system health