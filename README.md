# AsyncEndpoints

![AsyncEndpoints Logo](https://raw.githubusercontent.com/kaushik2901/async-endpoints/74749001092f6ee4f4bd52a1f19b376d4a69da2d/async-endpoints-banner.png "AsyncEndpoints")

[![Build](https://github.com/kaushik2901/async-endpoints/actions/workflows/build.yml/badge.svg)](https://github.com/kaushik2901/async-endpoints/actions/workflows/build.yml)

A modern .NET library for building asynchronous APIs that handle long-running operations in the background. AsyncEndpoints provides a clean, efficient solution for processing time-consuming tasks without blocking the client, using a producer-consumer pattern with configurable storage and retry mechanisms.

For complete documentation and more details, visit <a href="https://asyncendpoints.com" target="_blank">asyncendpoints.com</a>.

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

### Basic Setup

Configure services in your `Program.cs` and define async endpoints:

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

### Simple Handler Example

Implement your business logic with access to full HTTP context:

```csharp
using AsyncEndpoints.Handlers;

public class ProcessDataHandler(ILogger<ProcessDataHandler> logger) 
    : IAsyncEndpointRequestHandler<DataRequest, ProcessResult>
{
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        var request = context.Request;
        
        logger.LogInformation("Processing data request: {Data}", request.Data);

        // Perform your long-running processing here
        await Task.Delay(TimeSpan.FromSeconds(5), token); // Simulate work
        
        var result = new ProcessResult
        {
            ProcessedData = $"Processed: {request.Data.ToUpper()}",
            ProcessedAt = DateTime.UtcNow,
            CharacterCount = request.Data.Length
        };
            
        return MethodResult<ProcessResult>.Success(result);
    }
}
```

For detailed examples and advanced usage, visit our <a href="https://asyncendpoints.com" target="_blank">documentation website</a>.

## Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details on development setup, coding standards, and submission process.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.