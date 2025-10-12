# Interface Design: Supporting Request Without Body in AsyncEndpoints

## Overview

Currently, AsyncEndpoints requires a request body to be present when processing asynchronous endpoints. This documentation outlines the interface design for supporting requests without body while maintaining performance and scalability.

## Proposed Interface Design for Request Without Body

### 1. Base AsyncContext Type for No-Body Requests

We need a base `AsyncContext` type for requests without body data that will be inherited by the generic version. The base context should have consistent property names with the existing implementation:

```csharp
/// <summary>
/// Represents the context for an asynchronous request without a body, containing HTTP context information.
/// </summary>
/// <param name="headers">The HTTP headers from the original request.</param>
/// <param name="routeParams">The route parameters from the original request.</param>
/// <param name="queryParams">The query parameters from the original request.</param>
public class AsyncContext(
    IDictionary<string, List<string?>> headers,
    IDictionary<string, object?> routeParams,
    IEnumerable<KeyValuePair<string, List<string?>>> queryParams)
{
    /// <summary>
    /// Gets the HTTP headers from the original request.
    /// </summary>
    public IDictionary<string, List<string?>> Headers { get; init; } = headers;

    /// <summary>
    /// Gets or sets the route parameters from the original request.
    /// </summary>
    public IDictionary<string, object?> RouteParams { get; set; } = routeParams;

    /// <summary>
    /// Gets the query parameters from the original request.
    /// </summary>
    public IEnumerable<KeyValuePair<string, List<string?>>> QueryParams { get; init; } = queryParams;
}
```

**Note**: This base class allows us to use the same method names for both with-body and without-body operations since the generic and non-generic versions can coexist without conflict.

### 2. Updated Generic AsyncContext Type

The existing generic `AsyncContext<TRequest>` will inherit from the base `AsyncContext` to reuse common properties:

```csharp
/// <summary>
/// Represents the context for an asynchronous request, containing the request object and associated HTTP context information.
/// </summary>
/// <typeparam name="TRequest">The type of the request object.</typeparam>
/// <param name="request">The original request object.</param>
/// <param name="headers">The HTTP headers from the original request.</param>
/// <param name="routeParams">The route parameters from the original request.</param>
/// <param name="queryParams">The query parameters from the original request.</param>
public sealed class AsyncContext<TRequest>(
    TRequest request,
    IDictionary<string, List<string?>> headers,
    IDictionary<string, object?> routeParams,
    IEnumerable<KeyValuePair<string, List<string?>>> queryParams) : AsyncContext(headers, routeParams, queryParams)
{
    /// <summary>
    /// Gets the original request object.
    /// </summary>
    public TRequest Request { get; init; } = request;
}
```

### 3. Updated Route Mapping Extensions

We need to provide non-generic extension methods that handle requests without body (no conflict with existing generic methods). The implementation must bypass the JSON parsing step:

```csharp
public static class RouteBuilderExtensions
{
    /// <summary>
    /// Maps an asynchronous POST endpoint that processes requests without body in the background.
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
    /// <param name="jobName">A unique name for the async job, used for identifying the handler.</param>
    /// <param name="pattern">The URL pattern for the endpoint.</param>
    /// <param name="handler">Optional custom handler function to process the request.</param>
    /// <returns>An <see cref="IEndpointConventionBuilder"/> that can be used to further configure the endpoint.</returns>
    public static IEndpointConventionBuilder MapAsyncPost(
        this IEndpointRouteBuilder endpoints,
        string jobName,
        string pattern,
        Func<HttpContext, CancellationToken, Task<IResult?>?>? handler = null)
    {
        return endpoints
            .MapPost(pattern, async (HttpContext httpContext, [FromServices] IAsyncEndpointRequestDelegate asyncEndpointRequestDelegate, [FromServices] AsyncEndpointsConfigurations asyncEndpointsConfigurations, CancellationToken cancellationToken) =>
            {
                try
                {
                    return await asyncEndpointRequestDelegate.HandleAsync(jobName, httpContext, handler, cancellationToken);
                }
                catch (Exception ex)
                {
                    return await asyncEndpointsConfigurations.ResponseConfigurations.ExceptionResponseFactory(ex, httpContext);
                }
            })
            .WithTags(AsyncEndpointsConstants.AsyncEndpointTag);
    }
}
```

### 4. Updated AsyncEndpointRequestDelegate Interface

We need to update the interface to include a method for handling no-body requests:

```csharp
/// <summary>
/// Defines a contract for handling asynchronous endpoint requests and managing their lifecycle.
/// </summary>
public interface IAsyncEndpointRequestDelegate
{
    /// <summary>
    /// Handles an asynchronous request by creating a job and returning an immediate response.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request object.</typeparam>
    /// <param name="jobName">The unique name of the job, used to identify the specific handler.</param>
    /// <param name="httpContext">The HTTP context containing the request information.</param>
    /// <param name="request">The request object to process asynchronously.</param>
    /// <param name="handler">Optional custom handler function to process the request.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>An <see cref="IResult"/> representing the HTTP response.</returns>
    Task<IResult> HandleAsync<TRequest>(string jobName, HttpContext httpContext, TRequest request, Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Handles an asynchronous request without body data by creating a job and returning an immediate response.
    /// </summary>
    /// <param name="jobName">The unique name of the job, used to identify the specific handler.</param>
    /// <param name="httpContext">The HTTP context containing the request information.</param>
    /// <param name="handler">Optional custom handler function to process the request.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>An <see cref="IResult"/> representing the HTTP response.</returns>
    Task<IResult> HandleAsync(string jobName, HttpContext httpContext, Func<HttpContext, CancellationToken, Task<IResult?>?>? handler = null, CancellationToken cancellationToken = default);
}
```

### 5. New Handler Interface for No-Body Requests

We need a specialized handler interface for processing requests without body:

```csharp
/// <summary>
/// Defines a contract for handling asynchronous endpoint requests without body data, returning responses of type TResponse.
/// </summary>
/// <typeparam name="TResponse">The type of the response object.</typeparam>
public interface IAsyncEndpointRequestHandler<TResponse>
{
    /// <summary>
    /// Handles the asynchronous request without body data and returns a result.
    /// </summary>
    /// <param name="context">The context containing HTTP context information.</param>
    /// <param name="token">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="MethodResult{TResponse}"/> containing the result of the operation.</returns>
    Task<MethodResult<TResponse>> HandleAsync(AsyncContext context, CancellationToken token);
}
```

### 6. Dependency Injection Registration

We need to add a service registration method for handlers without request body. Since there's already a generic `AddAsyncEndpointHandler<THandler, TRequest, TResponse>` for with-body handlers, we can use the same name with different generic parameters to distinguish between them:

```csharp
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds an asynchronous endpoint handler for requests without body to the service collection.
    /// </summary>
    /// <typeparam name="THandler">The type of the handler implementation.</typeparam>
    /// <typeparam name="TResponse">The type of the response object.</typeparam>
    /// <param name="services">The service collection to add the handler to.</param>
    /// <param name="jobName">A unique name for the async job, used for identifying the handler.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddAsyncEndpointHandler<THandler, TResponse>(
        this IServiceCollection services, 
        string jobName) 
        where THandler : class, IAsyncEndpointRequestHandler<TResponse>
    {
        // Register the handler with the service provider
        services.AddScoped<THandler>();
        
        // Register job name mapping to handler type (implementation details may vary)
        // This could involve mapping job names to handler types in a configuration
        
        return services;
    }
}
```

**Note**: The method name is the same as the with-body version but with different generic parameters, allowing both to coexist without conflict due to method overloading based on generics.

### 7. Example Usage Interface

Developers should be able to define handlers for requests without body like this:

```csharp
// Handler that processes requests without body
public class NoBodyRequestHandler : IAsyncEndpointRequestHandler<string>
{
    public async Task<MethodResult<string>> HandleAsync(AsyncContext context, CancellationToken token)
    {
        // Process the request without body data
        // Access route parameters, query params, headers as needed
        var routeParam = context.RouteParams.ContainsKey("id") ? context.RouteParams["id"]?.ToString() : null;
        var queryParam = context.QueryParams.FirstOrDefault(x => x.Key == "action").Value?.FirstOrDefault();
        
        return MethodResult<string>.Success($"Processed request with id: {routeParam}, action: {queryParam}");
    }
}

// Usage in application startup
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsInMemoryStore()
    .AddAsyncEndpointsWorker()
    // Register the no-body handler
    .AddAsyncEndpointHandler<NoBodyRequestHandler, string>("simple-job");

var app = builder.Build();

// Map the no-body endpoint
app.MapAsyncPost("simple-job", "/simple-endpoint/{id}?action"); // Use registered handler or provide inline handler

await app.RunAsync();
```

## Design Considerations

### 1. Backward Compatibility
- Existing endpoints with bodies continue to work unchanged
- New methods for no-body endpoints are additive (no breaking changes)
- No changes required to existing handlers or configurations

### 2. Type Safety
- Clear distinction between handlers with and without body data
- Maintain compile-time type safety
- Prevent mixing of with-body and without-body request handlers

### 3. API Consistency
- Follow the same naming patterns as existing methods
- Use consistent parameter ordering
- Maintain the same response patterns and configurations

### 4. Developer Experience
- Clear method names that indicate no-body behavior
- Consistent with existing AsyncEndpoints patterns
- Intuitive for developers to understand and use

### 5. Implementation Details (Missing in Original Design)
- **JSON Parsing Bypass**: No-body endpoints must skip JSON parsing entirely
- **Context Creation**: Context should be created directly from HTTP context without body parsing
- **Service Registration**: Need specific registration method for no-body handlers
- **Job Payload Handling**: Handle empty/meaningful payloads for no-body jobs
- **Error Handling**: Consistent error handling across with-body and no-body endpoints
- **Configuration Consistency**: No-body endpoints should follow the same configuration patterns

## Performance Considerations in Interface Design

### 1. Memory Efficiency
- **Context Creation**: `AsyncContext` without body eliminates unnecessary request object allocation
- **Serialization Overhead**: No serialization needed when no body is processed

### 2. CPU Optimizations
- **Parsing Efficiency**: Interface design should allow for skipping JSON parsing when no body is present
- **Request Processing**: Streamlined processing for no-body requests

### 3. Request Processing
- **Faster Processing**: Interface should support streamlined processing for no-body endpoints
- **Optimized Paths**: Design interfaces that allow for optimized execution paths

## Scalability Considerations in Interface Design

### 1. Request Throughput
- **Higher Throughput**: Interface design should facilitate efficient processing for fire-and-forget operations
- **Reduced Bandwidth**: No-body requests consume less network resources

### 2. Resource Usage
- **Memory Footprint**: Interface should support minimal memory footprint for requests without body data
- **Connection Handling**: Interface design should enable efficient connection utilization

### 3. Distributed Processing
- **Job Queue Efficiency**: Interface should support smaller job payloads when no body data is needed
- **Processing Efficiency**: Interface design should allow for faster processing in distributed systems

## Additional Missing Details

### 1. Validation for Body Presence
The original design didn't address how to handle cases where a request body might be present but should be ignored. This implementation ensures the body is never parsed for no-body endpoints.

### 2. Configuration Consistency
No-body requests should use the same configuration system (retry logic, job timeouts, etc.) as regular requests to maintain consistency.

### 3. Type Safety Considerations
The interface design ensures that no-body handlers cannot be used with body endpoints and vice versa, preventing runtime errors.

### 4. Error Handling Consistency
The implementation maintains the same error handling patterns as existing endpoints, ensuring a consistent experience.

## Summary

The interface design for supporting requests without body in AsyncEndpoints focuses on:
1. Maintaining backward compatibility with existing functionality
2. Using inheritance: `AsyncContext<TRequest>` inherits from base `AsyncContext` to reuse common properties
3. Providing a dedicated handler interface `IAsyncEndpointRequestHandler<TResponse>` for no-body processing
4. Using same-named `MapAsyncPost` and `MapAsyncGet` methods with different signatures (no conflict with existing generic versions due to method overloading)
5. Ensuring type safety and compile-time verification
6. Designing for performance and scalability from the start
7. Adding comprehensive service registration and dependency injection support with same-named methods differentiated by generics
8. Maintaining consistent error handling and configuration patterns
9. Bypassing JSON parsing to avoid unnecessary overhead for no-body requests
10. Leveraging C# generics to allow clean method overloading between with-body and no-body operations

This design provides a clean separation between endpoints that require request bodies and those that don't, while maintaining consistency with the existing AsyncEndpoints patterns and using intuitive naming conventions.