---
sidebar_position: 3
---

# Quick Start

This guide will walk you through creating a simple application using AsyncEndpoints in 5 minutes.

## 1. Setup Services

First, configure AsyncEndpoints services in your `Program.cs`:

```csharp
using AsyncEndpoints.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAsyncEndpoints() // Core services
    .AddAsyncEndpointsInMemoryStore() // Development storage
    .AddAsyncEndpointsWorker(); // Background processing

// Register your job handlers
builder.Services.AddAsyncEndpointHandler<ProcessDataHandler, DataRequest, ProcessResult>("ProcessData");

var app = builder.Build();

// Map async endpoints
app.MapAsyncPost<DataRequest>("ProcessData", "/api/process-data");
app.MapAsyncGetJobDetails("/jobs/{jobId:guid}"); // Job status endpoint

await app.RunAsync();
```

## 2. Define Request and Response Models

Create your request and response models:

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

## 3. Create Request Handler

Implement your business logic:

```csharp
using AsyncEndpoints.Handlers;
using AsyncEndpoints.Utilities;

public class ProcessDataHandler(ILogger<ProcessDataHandler> logger) 
    : IAsyncEndpointRequestHandler<DataRequest, ProcessResult>
{
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        var request = context.Request;
        
        logger.LogInformation("Processing data request: {Data}", request.Data);

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
            
            logger.LogInformation("Successfully processed request");
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

## 4. Test Your Endpoint

Submit a job to your async endpoint:

```bash
curl -X POST http://localhost:5000/api/process-data \
  -H "Content-Type: application/json" \
  -d '{"data": "Hello, AsyncEndpoints!", "processingPriority": 2}'
```

You'll immediately receive a `202 Accepted` response with job details:

```json
{
  "id": "5b7e0e4a-8f8b-4c8a-9f1f-8d8f8e8f8e8f",
  "name": "ProcessData",
  "status": "Queued",
  "retryCount": 0,
  "maxRetries": 3,
  "createdAt": "2025-10-15T10:30:00.000Z",
  "startedAt": null,
  "completedAt": null,
  "lastUpdatedAt": "2025-10-15T10:30:00.000Z",
  "result": null
}
```

Check job status:

```bash
curl http://localhost:5000/jobs/5b7e0e4a-8f8b-4c8a-9f1f-8d8f8e8f8e8f
```

## 5. Production Setup

For production, use Redis storage:

```csharp
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";

builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsRedisStore(redisConnectionString) // Production storage
    .AddAsyncEndpointsWorker();
```

## What's Next?

- [Learn about configuration options](/docs/category/configuration)
- [Explore advanced features](/docs/category/advanced-features)
- [Check out the API reference](/docs/category/api-reference)