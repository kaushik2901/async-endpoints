# AsyncEndpoints

AsyncEndpoints is a .NET library that enables developers to easily build asynchronous APIs for processing long-running operations in the background. Instead of blocking the client while waiting for a task to complete, AsyncEndpoints queues the request and immediately responds with a 202 (Accepted) status containing a job ID and metadata for tracking the request's progress.

## Features

- **Asynchronous Processing**: Long-running operations are processed in the background without blocking the client
- **Job Status Tracking**: Monitor the status of your asynchronous jobs through dedicated endpoints
- **Retry Logic**: Failed jobs are retried automatically based on configured settings
- **Multiple Storage Options**: Support for in-memory and Redis storage backends
- **Background Workers**: Built-in background service for processing queued jobs
- **Cancellation Support**: Ability to cancel jobs in progress

## Installation

Install the core AsyncEndpoints NuGet package (Yet to be published in public):

```bash
dotnet add package AsyncEndpoints
```

For Redis support, also install the Redis extension package:

```bash
dotnet add package AsyncEndpoints.Redis
```

## Quick Start

### 1. Setup in Program.cs

```csharp
using AsyncEndpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsInMemoryStore() // Use in-memory store for development
    .AddAsyncEndpointsWorker() // Add background worker
    .AddAsyncEndpointHandler<AsyncEndpointHandler<Request, Response>, Request, Response>("MyJobName"); // Register handlers

var app = builder.Build();

// Define an async endpoint
app.MapAsyncPost<Request>("MyJobName", "/api/async-operation");

await app.RunAsync();
```

### 2. Create Request and Response Models

```csharp
public class Request
{
    public string Data { get; set; } = string.Empty;
}

public class Response
{
    public string ProcessedData { get; set; } = string.Empty;
}
```

### 3. Create a Handler

```csharp
using AsyncEndpoints.Contracts;
using AsyncEndpoints.Utilities;

public class AsyncEndpointHandler<Request, Response>(ILogger<AsyncEndpointHandler<Request, Response>> logger) 
    : IAsyncEndpointRequestHandler<Request, Response>
{
    public async Task<MethodResult<Response>> HandleAsync(AsyncContext<Request> context, CancellationToken token)
    {
        var requestData = context.Request;
        var headers = context.Headers;
        var queryParams = context.QueryParams;
        var routeParams = context.RouteParams;

        logger.LogInformation("Processing request with data: {Data}", requestData.Data);

        // Simulate async processing
        await Task.Delay(5000, token);

        var result = new Response 
        { 
            ProcessedData = $"Processed: {requestData.Data}" 
        };

        logger.LogInformation("Request processed successfully.");
        return MethodResult<Response>.Success(result);
    }
}
```

### 4. Make an Async Request

Send a POST request to your async endpoint:

```bash
curl -X POST http://localhost:5000/api/async-operation \\
  -H \"Content-Type: application/json\" \\
  -d '{\"data\": \"Hello Async World\"}'
```

You'll receive a 202 (Accepted) response immediately with job details:

```json
{
    "id": "a1b2c3d4-e5f6-7890-1234-567890abcdef",
    "name": "MyJobName",
    "status": "Queued",
    "retryCount": 0,
    "maxRetries": 3,
    "createdAt": "2025-09-25T10:30:00.000Z",
    "startedAt": null,
    "completedAt": null,
    "lastUpdatedAt": "2025-09-25T10:30:00.000Z",
    "result": null,
    "exception": null
}
```

## Configuration

You can customize AsyncEndpoints behavior by configuring options:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    // Job Manager Configuration
    options.JobManagerConfiguration.DefaultMaxRetries = 5; // Set default maximum retry attempts
    options.JobManagerConfiguration.RetryDelayBaseSeconds = 3.0; // Set base delay for retry exponential backoff
    options.JobManagerConfiguration.MaxConcurrentJobs = 20; // Set maximum number of concurrent jobs
    options.JobManagerConfiguration.JobPollingIntervalMs = 2000; // Set polling interval for job polling
    
    // Worker Configuration
    options.WorkerConfigurations.MaximumConcurrency = 10; // Set max concurrent jobs
    options.WorkerConfigurations.PollingIntervalMs = 500; // Set polling interval
    options.WorkerConfigurations.JobTimeoutMinutes = 60; // Set job timeout
    options.WorkerConfigurations.BatchSize = 10; // Set maximum number of jobs to process in a single batch
    options.WorkerConfigurations.MaximumQueueSize = 100; // Set maximum size of the job queue
});
```

## Storage Providers

AsyncEndpoints supports multiple storage backends:

### In-Memory Store (Development)

```csharp
builder.Services.AddAsyncEndpointsInMemoryStore();
```

> Note: In-memory store is only suitable for development or single-instance deployments.

### Redis Store (Production)

For production applications that require persistence and distributed processing, add the Redis store:

```csharp
// Option 1: Using connection string
builder.Services.AddAsyncEndpointsRedisStore("localhost:6379");

// Option 2: Using IConnectionMultiplexer
var connectionMultiplexer = ConnectionMultiplexer.Connect("localhost:6379");
builder.Services.AddAsyncEndpointsRedisStore(connectionMultiplexer);

// Option 3: Using configuration action
builder.Services.AddAsyncEndpointsRedisStore(config =>
{
    config.ConnectionString = "localhost:6379";
    config.ConfigurationOptions = new ConfigurationOptions
    {
        AbortOnConnectFail = false,
        ConnectRetry = 3,
        ConnectTimeout = 5000,
        SyncTimeout = 5000
    };
});
```

> Note: To use Redis store, you need to install the `AsyncEndpoints.Redis` package separately.

### Job Status

When a job is submitted, you'll receive a 202 (Accepted) response with job details including the job ID. You can then monitor the status of your job using the returned job information.

The response includes these properties:

- `id`: Unique identifier for the job
- `name`: Name of the job type
- `status`: Current status of the job
- `retryCount`: Number of retry attempts made
- `maxRetries`: Maximum number of retry attempts allowed
- `createdAt`: When the job was created
- `startedAt`: When the job started processing (nullable)
- `completedAt`: When the job completed (nullable)
- `lastUpdatedAt`: When the job status was last updated
- `result`: The result of the job after completion (nullable)
- `exception`: Exception information if the job failed (nullable)

### Possible Job States

- `Queued`: Job is waiting to be processed
- `Scheduled`: Job is scheduled for delayed execution
- `InProgress`: Job is currently being executed
- `Completed`: Job completed successfully
- `Failed`: Job failed after all retry attempts
- `Canceled`: Job was canceled before completion

## Middleware Support

You can execute custom logic before a job is queued using middleware functions:

```csharp
app.MapAsyncPost<Request>("MyJobName", "/api/async-operation", 
    async (httpContext, request, token) => 
    {
        // Perform validation, authentication, or other synchronous tasks
        // Return a specific result to prevent queuing (non-null value)
        // Return null to continue queuing the job
        if (string.IsNullOrEmpty(request.Data))
        {
            return Results.BadRequest("Data is required");
        }
        
        return null; // Continue to queue the job
    });
```

## Best Practices

1. **Use Appropriate Storage**: For production applications, use Redis storage instead of the in-memory store
2. **Handle Errors Gracefully**: Implement proper error handling in your handlers
3. **Validate Requests**: Use middleware to validate requests before queuing
4. **Set Appropriate Timeouts**: Configure job timeouts based on your expected processing times
5. **Monitor and Log**: Use structured logging to track job processing

## Project Structure

The library is organized into two main projects:

- **AsyncEndpoints**: Core functionality including job management, background workers, and in-memory storage
- **AsyncEndpoints.Redis**: Redis-based job storage implementation for distributed environments

## Architecture

AsyncEndpoints is built with the following components:

- **Job Producer**: Queues requests to be processed asynchronously
- **Background Workers**: Process queued jobs in the background
- **Job Store**: Persists job information and state (with implementations for in-memory and Redis)
- **Handlers**: Business logic for processing requests
- **Endpoints**: API endpoints for submitting jobs

## Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for more details.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.