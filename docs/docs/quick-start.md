---
sidebar_position: 3
title: Quick Start
---

# Quick Start

This guide walks you through creating your first async endpoint using AsyncEndpoints. By the end of this guide, you'll have a working example that processes requests in the background and allows status tracking.

## Prerequisites

Before starting, make sure you've installed the AsyncEndpoints package as described in the [Installation](./installation.md) guide.

## Step 1: Create Your Request and Response Models

First, define the data models for your async operation:

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

## Step 2: Create the Request Handler

Next, implement the request handler that will process your async operations:

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
            
            logger.LogInformation("Successfully processed request for data: {Data}", request.Data);
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

## Step 3: Configure Services in Program.cs

Configure the AsyncEndpoints services in your application's dependency injection container:

```csharp
using AsyncEndpoints;
using Microsoft.AspNetCore.Builder;

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

## Step 4: Test the Application

Start your application and test the endpoints:

1. **Submit a job** (POST request):
   ```bash
   curl -X POST http://localhost:5000/api/process-data \
     -H "Content-Type: application/json" \
     -d '{"data": "Hello, AsyncEndpoints!", "processingPriority": 2}'
   ```

2. **Check job status** (GET request with the returned job ID):
   ```bash
   curl http://localhost:5000/jobs/{jobId}
   ```

## Understanding the Response

When you submit a job, AsyncEndpoints immediately returns a response with job details:

```json
{
  "id": "5b7e0e4a-8f8b-4c8a-9f1f-8d8f8e8f8e8f",
  "name": "ProcessData",
  "status": "Queued",
  "retryCount": 0,
  "maxRetries": 3,
  "createdAt": "2025-10-14T10:30:00.000Z",
  "startedAt": null,
  "completedAt": null,
  "lastUpdatedAt": "2025-10-14T10:30:00.000Z",
  "result": null
}
```

Once processing completes, the status endpoint will return the result:

```json
{
  "id": "5b7e0e4a-8f8b-4c8a-9f1f-8d8f8e8f8e8f",
  "name": "ProcessData",
  "status": "Completed",
  "retryCount": 0,
  "maxRetries": 3,
  "createdAt": "2025-10-14T10:30:00.000Z",
  "startedAt": "2025-10-14T10:30:05.000Z",
  "completedAt": "2025-10-14T10:30:10.000Z",
  "lastUpdatedAt": "2025-10-14T10:30:10.000Z",
  "result": {
    "processedData": "PROCESSED: HELLO, ASYNCENDPOINTS!",
    "processedAt": "2025-10-14T10:30:10.000Z",
    "characterCount": 20
  }
}
```

## Adding Validation Middleware

You can also add custom validation before jobs are queued:

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

## Next Steps

Now that you've successfully created your first async endpoint, explore these topics:

- **[Core Concepts](./architecture.md)**: Understand the architecture and design patterns
- **[Configuration](./configuration/configuration.md)**: Learn how to configure AsyncEndpoints for your specific needs
- **[Advanced Features](./advanced-features.md)**: Explore advanced usage patterns
- **[Recipes and Examples](./file-processing.md)**: Learn implementation patterns for common use cases