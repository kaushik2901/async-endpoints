---
title: "Introduction"
description: "Learn about AsyncEndpoints, a modern .NET library for building asynchronous APIs that handle long-running operations in the background with job tracking and resilience."
keywords: ["async endpoints", ".NET asynchronous processing", "background jobs", "API queue", "job tracking", "distributed processing", "C# queue"]
sidebar_position: 1
---

# Introduction

**AsyncEndpoints** is a modern .NET library for building asynchronous APIs that handle long-running operations in the background with built-in queuing, status tracking, and multiple storage backends. It provides a clean, efficient solution for processing time-consuming tasks without blocking the client.

## What is AsyncEndpoints?

AsyncEndpoints allows you to build APIs that immediately respond to requests while processing them in the background. Instead of keeping clients waiting for long-running operations to complete, AsyncEndpoints accepts requests, returns a job ID immediately, and processes the work in background workers. Clients can then check the status of their job using the returned ID.

## Core Benefits

- **Non-blocking**: Return responses immediately without waiting for long operations
- **Job Status Tracking**: Monitor job progress with rich metadata through dedicated endpoints
- **Configurable Retry Logic**: Automatic retries with exponential backoff for failed jobs
- **Multiple Storage Backends**: Support for in-memory (development) and Redis (production) storage
- **Background Workers**: Built-in hosted service with configurable concurrency and queue limits
- **Distributed Recovery**: Automatic recovery of stuck jobs in multi-instance deployments
- **HTTP Context Preservation**: Maintains headers, route parameters, and query parameters through job lifecycle
- **Structured Error Handling**: Comprehensive error reporting and exception serialization

## Quick Example

Here's how to set up AsyncEndpoints in your application:

```csharp
using AsyncEndpoints;
using AsyncEndpoints.Extensions;

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

Define your request and response models:

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

Create your handler implementation:

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

## Next Steps

Continue to the next sections to learn how to install, configure, and use AsyncEndpoints in your projects.
