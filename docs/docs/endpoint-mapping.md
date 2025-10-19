---
sidebar_position: 2
title: Endpoint Mapping
---

# Endpoint Mapping

This page explains how to map asynchronous endpoints using AsyncEndpoints extension methods and how HTTP requests are processed through the async pipeline.

## Overview

AsyncEndpoints provides several extension methods to map HTTP endpoints that process requests asynchronously. These methods separate the immediate response from the background processing, allowing your application to remain responsive during long-running operations.

## Supported HTTP Methods

The library supports multiple HTTP methods through dedicated mapping methods:

### POST Endpoints

#### With Request Body
```csharp
app.MapAsyncPost<TRequest>(string jobName, string pattern, Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null)
```

Example:
```csharp
app.MapAsyncPost<DataRequest>("ProcessData", "/api/process-data");
```

#### Without Request Body
```csharp
app.MapAsyncPost(string jobName, string pattern, Func<HttpContext, NoBodyRequest, CancellationToken, Task<IResult?>?>? handler = null)
```

Example:
```csharp
app.MapAsyncPost("GenerateReport", "/api/generate-report");
```

### PUT Endpoints

#### With Request Body
```csharp
app.MapAsyncPut<TRequest>(string jobName, string pattern, Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null)
```

Example:
```csharp
app.MapAsyncPut<UpdateRequest>("UpdateData", "/api/update-data");
```

#### Without Request Body
```csharp
app.MapAsyncPut(string jobName, string pattern, Func<HttpContext, NoBodyRequest, CancellationToken, Task<IResult?>?>? handler = null)
```

### PATCH Endpoints

#### With Request Body
```csharp
app.MapAsyncPatch<TRequest>(string jobName, string pattern, Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null)
```

#### Without Request Body
```csharp
app.MapAsyncPatch(string jobName, string pattern, Func<HttpContext, NoBodyRequest, CancellationToken, Task<IResult?>?>? handler = null)
```

### DELETE Endpoints

#### With Request Body
```csharp
app.MapAsyncDelete<TRequest>(string jobName, string pattern, Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null)
```

#### Without Request Body
```csharp
app.MapAsyncDelete(string jobName, string pattern, Func<HttpContext, NoBodyRequest, CancellationToken, Task<IResult?>?>? handler = null)
```

### Job Status Endpoint

```csharp
app.MapAsyncGetJobDetails(string pattern = "/jobs/{jobId:guid}")
```

This endpoint allows clients to check job status using the job ID returned from the initial request.

## Parameter Mapping

### Route Parameters
Route parameters are automatically captured and preserved in the job context:

```csharp
app.MapAsyncPost<DataRequest>("ProcessData", "/api/process-data/{id}/action");

// In your handler, access route parameters:
public class ProcessDataHandler(ILogger<ProcessDataHandler> logger) 
    : IAsyncEndpointRequestHandler<DataRequest, ProcessResult>
{
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        var routeParams = context.RouteParams;
        var id = routeParams["id"]; // Access the captured route parameter
        // Process with the route parameter value
        return MethodResult<ProcessResult>.Success(new ProcessResult());
    }
}
```

### Query Parameters
Query parameters are also preserved in the job context:

```csharp
// Request: /api/process-data?format=json&priority=high
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
{
    var queryParams = context.QueryParams;
    var format = queryParams.FirstOrDefault(q => q.Key == "format").Value?.FirstOrDefault();
    var priority = queryParams.FirstOrDefault(q => q.Key == "priority").Value?.FirstOrDefault();
    // Process with query parameter values
    return MethodResult<ProcessResult>.Success(new ProcessResult());
}
```

### HTTP Headers
HTTP headers are preserved and accessible in the handler:

```csharp
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
{
    var headers = context.Headers;
    var authorization = headers.GetValueOrDefault("Authorization", new List<string?>());
    // Process with header information
    return MethodResult<ProcessResult>.Success(new ProcessResult());
}
```

## Custom Validation Middleware

You can add custom validation before jobs are queued by providing a custom handler function:

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

In this pattern:
- Return a non-null `IResult` to terminate the request (validation failure)
- Return `null` to continue with job queuing
- The validation occurs synchronously before the job is submitted to the background queue

## Example with Multiple Endpoints

```csharp
// POST endpoint with request body
app.MapAsyncPost<CreateRequest>("CreateResource", "/api/resources");

// PUT endpoint with request body
app.MapAsyncPut<UpdateRequest>("UpdateResource", "/api/resources/{id}");

// PATCH endpoint without request body
app.MapAsyncPatch("RefreshResource", "/api/resources/{id}/refresh");

// DELETE endpoint with request body
app.MapAsyncDelete<DeleteRequest>("DeleteResource", "/api/resources/{id}");

// Job status endpoint
app.MapAsyncGetJobDetails("/jobs/{jobId:guid}");
```

## Error Handling During Mapping

The mapping methods include built-in error handling:

- **Serialization Errors**: Failed request serialization returns appropriate HTTP error responses
- **Handler Registration**: Invalid job names result in clear error messages
- **Route Conflicts**: Standard ASP.NET Core routing behavior applies

## Best Practices

### Use Descriptive Job Names
Choose clear, unique job names that identify the specific operation:

```csharp
// Good
app.MapAsyncPost<ProcessPaymentRequest>("ProcessPayment", "/api/payments/process");
app.MapAsyncPost<GenerateReportRequest>("GenerateMonthlyReport", "/api/reports/monthly");

// Avoid
app.MapAsyncPost<Request>("Job1", "/api/jobs/1");
app.MapAsyncPost<Request>("HandleRequest", "/api/process");
```

### Validate Critical Parameters Early
Use validation middleware to catch common errors before queuing:

```csharp
app.MapAsyncPost<DataRequest>("ProcessData", "/api/process-data", 
    async (HttpContext context, DataRequest request, CancellationToken token) => 
    {
        // Validate critical parameters that would cause early failure
        if (string.IsNullOrWhiteSpace(request.Data))
        {
            return Results.BadRequest("Data field is required");
        }
        
        // Additional validation as needed
        return null;
    });
```

### Consider Security Implications
Use appropriate authentication and authorization middleware:

```csharp
app.MapAsyncPost<DataRequest>("ProcessData", "/api/process-data")
    .RequireAuthorization("AsyncDataProcessor"); // Apply authorization policy
```

The endpoint mapping functionality provides a clean, intuitive API for creating async endpoints while preserving the full HTTP context for background processing.