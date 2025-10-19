---
sidebar_position: 6
---

# Endpoint Mapping

## Basic Endpoint Mapping

Map asynchronous endpoints using the `MapAsyncPost` method:

```csharp
// Basic mapping with request body
app.MapAsyncPost<ExampleRequest>("ProcessData", "/api/process-data");

// Without request body
app.MapAsyncPost("GenerateReport", "/api/generate-report");
```

## Request With Body

For endpoints that accept a request body:

```csharp
// Register your handler
builder.Services.AddAsyncEndpointHandler<ProcessDataHandler, DataRequest, ProcessResult>("ProcessData");

// Map the endpoint
app.MapAsyncPost<DataRequest>("ProcessData", "/api/process-data");
```

The handler implementation:

```csharp
public class ProcessDataHandler : IAsyncEndpointRequestHandler<DataRequest, ProcessResult>
{
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        var request = context.Request;
        // Process request...
        return MethodResult<ProcessResult>.Success(result);
    }
}
```

## Request Without Body

For endpoints that don't require a request body:

```csharp
// Register handler without request body
builder.Services.AddAsyncEndpointHandler<GenerateReportHandler, ReportResult>("GenerateReport");

// Map endpoint without type parameter
app.MapAsyncPost("GenerateReport", "/api/generate-report");

// Handler implementation
public class GenerateReportHandler : IAsyncEndpointRequestHandler<ReportResult>
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
```

## Custom Validation Middleware

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

## Route Parameters

Access route parameters in your handlers:

```csharp
// Map with route parameters
app.MapAsyncPost<DataRequest>("ProcessResource", "/api/resources/{resourceId}/process");

// In your handler, access route parameters
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
{
    var resourceId = context.RouteParams["resourceId"]?.ToString();
    // Use the route parameter...
}
```

## Query Parameters

Query parameters are preserved and available in handlers:

```csharp
// Client request: /api/process?format=json&priority=high
app.MapAsyncPost<DataRequest>("ProcessData", "/api/process-data");

// In your handler
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
{
    var queryParams = context.QueryParams;
    var format = queryParams.FirstOrDefault(x => x.Key == "format").Value?.FirstOrDefault();
    var priority = queryParams.FirstOrDefault(x => x.Key == "priority").Value?.FirstOrDefault();
    // Use query parameters...
}
```

## HTTP Headers

HTTP headers are preserved and accessible in handlers:

```csharp
public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
{
    var headers = context.Headers;
    var authorization = headers["Authorization"]?.FirstOrDefault();
    var userAgent = headers["User-Agent"]?.FirstOrDefault();
    // Use headers...
}
```

## Status Endpoint

Map the job status endpoint to check job progress:

```csharp
// Default pattern: /jobs/{jobId:guid}
app.MapAsyncGetJobDetails();

// Custom pattern
app.MapAsyncGetJobDetails("/status/{jobId:guid}");
```

## Complete Example

Here's a complete example showing various mapping scenarios:

```csharp
using AsyncEndpoints.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsInMemoryStore()
    .AddAsyncEndpointsWorker()
    .AddAsyncEndpointHandler<ProcessDataHandler, DataRequest, ProcessResult>("ProcessData")
    .AddAsyncEndpointHandler<GenerateReportHandler, ReportResult>("GenerateReport")
    .AddAsyncEndpointHandler<ProcessResourceHandler, ResourceRequest, ResourceResult>("ProcessResource");

var app = builder.Build();

// Job status endpoint
app.MapAsyncGetJobDetails("/jobs/{jobId:guid}");

// Basic endpoint with request body
app.MapAsyncPost<DataRequest>("ProcessData", "/api/process-data");

// Endpoint with validation
app.MapAsyncPost<ResourceRequest>("ProcessResource", "/api/resources/{resourceId}/process",
    async (HttpContext context, ResourceRequest request, CancellationToken token) =>
    {
        var resourceId = context.Request.RouteValues["resourceId"]?.ToString();
        if (string.IsNullOrEmpty(resourceId))
        {
            return Results.BadRequest("Resource ID is required");
        }
        return null; // Continue processing
    });

// Endpoint without request body
app.MapAsyncPost("GenerateReport", "/api/generate-report");

await app.RunAsync();
```

## Error Handling

The framework handles errors automatically, but you can customize responses:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.ResponseConfigurations.ExceptionResponseFactory = async (exception, context) =>
    {
        // Log the exception
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(exception, "Error in async endpoint");
        
        // Return custom error response
        return Results.Problem(
            title: "Processing Error",
            detail: "An error occurred while processing your request",
            statusCode: 500
        );
    };
});
```

## Best Practices

1. **Unique Job Names**: Use descriptive, unique names for each job type
2. **Validation**: Implement proper validation before queuing jobs
3. **Error Handling**: Handle errors gracefully in your handlers
4. **Parameter Access**: Make full use of HTTP context information in handlers
5. **Security**: Validate and sanitize all input data
6. **Performance**: Consider validation middleware to prevent unnecessary job creation