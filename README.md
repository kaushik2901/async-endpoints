# AsyncEndpoints

![AsyncEndpoints Logo](async-endpoints-banner.png "AsyncEndpoints")

A modern .NET library for building asynchronous APIs that handle long-running operations in the background. AsyncEndpoints provides a clean, efficient solution for processing time-consuming tasks without blocking the client, using a producer-consumer pattern with configurable storage and retry mechanisms.

## Key Features

- **Asynchronous Processing**: Execute long-running operations in the background without blocking clients
- **Job Status Tracking**: Monitor job progress through dedicated endpoints with rich metadata
- **Configurable Retry Logic**: Automatic retries with exponential backoff for failed jobs
- **Multiple Storage Backends**: Support for in-memory (development) and Redis (production) storage
- **Background Workers**: Built-in hosted service with configurable concurrency and queue limits
- **Distributed Recovery**: Automatic recovery of stuck jobs in multi-instance deployments
- **HTTP Context Preservation**: Maintains headers, route parameters, and query parameters through job lifecycle
- **Structured Error Handling**: Comprehensive error reporting and exception serialization
- **Circuit Breaker Pattern**: Prevents system overload with configurable queue limits

## Installation

Install the core AsyncEndpoints package:

```bash
dotnet add package AsyncEndpoints
```

For Redis support, also install the Redis extension:

```bash
dotnet add package AsyncEndpoints.Redis
```

## Getting Started

### 1. Basic Setup

Configure services in your `Program.cs`:

```csharp
using AsyncEndpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAsyncEndpoints() // Core services
    .AddAsyncEndpointsInMemoryStore() // Development storage
    .AddAsyncEndpointsWorker(); // Background processing

// Register job handlers
builder.Services.AddAsyncEndpointHandler<ProcessDataHandler, DataRequest, ProcessResult>("ProcessData");

var app = builder.Build();

// Define async endpoints
app.MapAsyncPost<DataRequest>("ProcessData", "/api/process-data");
app.MapAsyncGetJobDetails("/jobs/{jobId:guid}"); // Job status endpoint

await app.RunAsync();
```

### 2. Define Request and Response Models

```csharp
public class DataRequest
{
    public string Data { get; set; } = string.Empty;
    public int ProcessingPriority { get; set; } = 1;
}

public class ProcessResult
{
    public string ProcessedData { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public int CharacterCount { get; set; }
}
```

### 3. Create Request Handler

Implement your business logic with access to full HTTP context:

```csharp
using AsyncEndpoints.Handlers;
using AsyncEndpoints.Utilities;

public class ProcessDataHandler(ILogger<ProcessDataHandler> logger) 
    : IAsyncEndpointRequestHandler<DataRequest, ProcessResult>
{
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        var request = context.Request;
        
        // Access HTTP context information
        var headers = context.Headers;
        var routeParams = context.RouteParams;
        var queryParams = context.QueryParams;
        
        logger.LogInformation("Processing data request: {Data} with priority {Priority}", 
            request.Data, request.ProcessingPriority);

        try
        {
            // Simulate processing time
            await Task.Delay(TimeSpan.FromSeconds(5), token);
            
            // Perform actual processing
            var result = new ProcessResult
            {
                ProcessedData = $"Processed: {request.Data.ToUpper()}",
                ProcessedAt = DateTime.UtcNow,
                CharacterCount = request.Data.Length
            };
            
            logger.LogInformation("Successfully processed request with ID");
            return MethodResult<ProcessResult>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing request: {Data}", request.Data);
            return MethodResult<ProcessResult>.Failure(ex);
        }
    }
}
```

### 4. Client Integration

Submit jobs and track their status with a single POST request:

```bash
# Submit a job
curl -X POST http://localhost:5000/api/process-data \
  -H "Content-Type: application/json" \
  -d '{"data": "Hello, AsyncEndpoints!", "processingPriority": 2}'

# Response immediately returns 202 Accepted with job details:
# {
#   "id": "5b7e0e4a-8f8b-4c8a-9f1f-8d8f8e8f8e8f",
#   "name": "ProcessData",
#   "status": "Queued",
#   "retryCount": 0,
#   "maxRetries": 3,
#   "createdAt": "2025-10-14T10:30:00.000Z",
#   "startedAt": null,
#   "completedAt": null,
#   "lastUpdatedAt": "2025-10-14T10:30:00.000Z",
#   "result": null
# }
```

Monitor job status using the provided job ID:

```bash
# Check job status
curl http://localhost:5000/jobs/5b7e0e4a-8f8b-4c8a-9f1f-8d8f8e8f8e8f
```

## Configuration

### Core Settings

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    // Worker configuration (background processing)
    options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount; // Default: CPU count
    options.WorkerConfigurations.PollingIntervalMs = 1000; // Default: 1 second
    options.WorkerConfigurations.JobTimeoutMinutes = 30; // Default: 30 minutes
    options.WorkerConfigurations.BatchSize = 5; // Default: 5 jobs per batch
    options.WorkerConfigurations.MaximumQueueSize = 50; // Default: 50 jobs max
    
    // Job manager configuration (retry and processing logic)
    options.JobManagerConfiguration.DefaultMaxRetries = 3; // Default: 3 retries
    options.JobManagerConfiguration.RetryDelayBaseSeconds = 2.0; // Default: 2s (exponential backoff)
    options.JobManagerConfiguration.MaxConcurrentJobs = 10; // Default: 10 concurrent jobs
    options.JobManagerConfiguration.JobPollingIntervalMs = 1000; // Default: 1 second
    options.JobManagerConfiguration.MaxClaimBatchSize = 10; // Default: 10 jobs per claim batch
    options.JobManagerConfiguration.JobClaimTimeout = TimeSpan.FromMinutes(5); // Default: 5 minutes
    options.JobManagerConfiguration.StaleJobClaimCheckInterval = TimeSpan.FromMinutes(1); // Default: 1 minute
});
```

### Distributed Recovery Configuration

Enable automatic recovery for stuck jobs in distributed environments:

```csharp
builder.Services.AddAsyncEndpointsWorker(recoveryConfiguration =>
{
    recoveryConfiguration.EnableDistributedJobRecovery = true; // Default: true
    recoveryConfiguration.JobTimeoutMinutes = 30; // Default: 30 minutes
    recoveryConfiguration.RecoveryCheckIntervalSeconds = 300; // Default: 5 minutes
    recoveryConfiguration.MaximumRetries = 3; // Default: 3 retries
});
```

### Response Customization

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
        if (jobResult.IsSuccess)
        {
            return Results.Ok(jobResult.Data);
        }
        return Results.Problem("Job not found", statusCode: 404);
    };
});
```

## Storage Options

### In-Memory Storage (Development)

Suitable for single-instance deployments and development:

```csharp
builder.Services.AddAsyncEndpointsInMemoryStore();
```

> **Note**: Data is lost when the application restarts. Use only for development.

### Redis Storage (Production)

Distributed storage with persistence and multi-instance support:

```csharp
// Option 1: Connection string
builder.Services.AddAsyncEndpointsRedisStore("localhost:6379");

// Option 2: Connection multiplexer
var connection = ConnectionMultiplexer.Connect("localhost:6379");
builder.Services.AddAsyncEndpointsRedisStore(connection);

// Option 3: Configuration object
builder.Services.AddAsyncEndpointsRedisStore(config =>
{
    config.ConnectionString = "localhost:6379";
    // Additional configuration options can be set here
});
```

## Job Status Lifecycle

Jobs progress through the following states:

- `Queued`: Job created and waiting for processing
- `Scheduled`: Job scheduled for delayed execution (with retry delays)
- `InProgress`: Currently being processed by a worker
- `Completed`: Successfully completed with result available
- `Failed`: Failed after all retry attempts exhausted
- `Canceled`: Explicitly canceled before completion

## Request Validation Middleware

Apply custom validation before jobs are queued:

```csharp
app.MapAsyncPost<DataRequest>("ProcessData", "/api/process-data", 
    async (HttpContext context, DataRequest request, CancellationToken token) => 
    {
        // Validate request before queuing
        if (string.IsNullOrWhiteSpace(request.Data))
        {
            return Results.BadRequest("Data field is required");
        }
        
        if (request.ProcessingPriority < 1 || request.ProcessingPriority > 5)
        {
            return Results.BadRequest("Priority must be between 1 and 5");
        }
        
        // Return null to continue queuing the job
        return null;
    });
```

## No-Body Request Handlers

For endpoints that don't require a request body:

```csharp
// Register handler without request body
builder.Services.AddAsyncEndpointHandler<GenerateReportHandler, ReportResult>("GenerateReport");

// Handler implementation
public class GenerateReportHandler(ILogger<GenerateReportHandler> logger)
    : IAsyncEndpointRequestHandler<ReportResult>
{
    public async Task<MethodResult<ReportResult>> HandleAsync(AsyncContext context, CancellationToken token)
    {
        // Access headers, route parameters, and query parameters
        var headers = context.Headers;
        var routeParams = context.RouteParams;
        var queryParams = context.QueryParams;
        
        // Process request without body data
        var result = new ReportResult 
        { 
            ReportData = "Generated report data...",
            GeneratedAt = DateTime.UtcNow
        };
        
        return MethodResult<ReportResult>.Success(result);
    }
}

// Map endpoint without type parameter
app.MapAsyncPost("GenerateReport", "/api/generate-report");
```

## Best Practices

1. **Production Deployment**: Use Redis storage with proper connection configuration
2. **Error Handling**: Implement comprehensive error handling in handlers using `MethodResult`
3. **Resource Management**: Configure appropriate concurrency and queue limits based on system capacity
4. **Timeout Configuration**: Set job timeouts based on expected processing times
5. **Monitoring**: Enable structured logging to track job processing and system health
6. **Security**: Validate and sanitize input data before queuing jobs
7. **Testing**: Test handlers with realistic data and failure scenarios
8. **Performance**: Monitor queue sizes and processing times to optimize configuration

## Architecture Overview

AsyncEndpoints follows a clean architectural pattern:

- **Job Store**: Abstracts persistence layer (In-Memory/Redis)
- **Job Manager**: Coordinates job state and retry logic
- **Background Service**: Producer-consumer pattern with configurable concurrency
- **Request Handlers**: Business logic with full HTTP context access
- **Endpoint Mappers**: Minimal API integration with ASP.NET Core

## Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details on development setup, coding standards, and submission process.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.