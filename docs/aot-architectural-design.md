# AOT-Compatible Architecture for AsyncEndpoints

## Overview

This document outlines an AOT (Ahead-of-Time) compatible architectural design for the AsyncEndpoints library that eliminates runtime reflection while maintaining the flexibility of the current system.

## Current Architecture Issues

The current architecture has the following AOT compatibility challenges:

1. **Runtime Type Resolution**: The system relies on `MakeGenericType()` calls which involve reflection
2. **Dynamic Method Invocation**: Generic method invocations use reflection APIs like `GetMethod()` and `MakeGenericMethod()`
3. **Runtime Handler Discovery**: Handlers are resolved at runtime using type information that is only available at runtime

## Proposed AOT-Compatible Architecture

### 1. Static Registration System

The new architecture will use a static registration system where invokers are pre-registered at startup time:

```csharp
public interface IHandlerInvoker<in TRequest, out TResponse>
{
    Task<MethodResult<TResponse>> ExecuteAsync(IServiceProvider serviceProvider, TRequest request, CancellationToken cancellationToken);
}

public static class AotHandlerRegistry
{
    private static readonly ConcurrentDictionary<string, Func<IServiceProvider, object, CancellationToken, Task<MethodResult<object>>>> _invokers = new();
    
    public static void Register<TRequest, TResponse>(string jobName, 
        Func<IServiceProvider, TRequest, CancellationToken, Task<MethodResult<TResponse>>> handlerFunc)
    {
        Func<IServiceProvider, object, CancellationToken, Task<MethodResult<object>>> invoker = 
            (serviceProvider, request, cancellationToken) =>
            {
                var typedRequest = (TRequest)request;
                var result = handlerFunc(serviceProvider, typedRequest, cancellationToken);
                return ConvertResultToObject(result);
            };
            
        _invokers.TryAdd(jobName, invoker);
    }
    
    public static Func<IServiceProvider, object, CancellationToken, Task<MethodResult<object>>>? GetInvoker(string jobName)
    {
        return _invokers.TryGetValue(jobName, out var invoker) ? invoker : null;
    }
}
```

### 2. Registration at Startup

Handlers would be registered at application startup using an extension method:

```csharp
public static class AotServiceCollectionExtensions
{
    public static IServiceCollection AddAsyncEndpointHandlerAot<THandler, TRequest, TResponse>(
        this IServiceCollection services, string jobName) 
        where THandler : class, IAsyncEndpointRequestHandler<TRequest, TResponse>
    {
        // Register the handler as a keyed service
        services.AddKeyedScoped<IAsyncEndpointRequestHandler<TRequest, TResponse>, THandler>(jobName);
        
        // Pre-register the invoker delegate
        AotHandlerRegistry.Register<TRequest, TResponse>(jobName, 
            async (serviceProvider, request, cancellationToken) =>
            {
                var handler = serviceProvider.GetKeyedService<IAsyncEndpointRequestHandler<TRequest, TResponse>>(jobName);
                if (handler == null)
                {
                    throw new InvalidOperationException($"Handler not found for job name: {jobName}");
                }
                
                var context = new AsyncContext<TRequest>(request);
                return await handler.HandleAsync(context, cancellationToken);
            });
        
        return services;
    }
}
```

### 3. AOT-Optimized Consumer Service

The consumer service would use the pre-registered invokers:

```csharp
public class AotJobConsumerService(
    ILogger<AotJobConsumerService> logger, 
    IJobStore jobStore, 
    IServiceProvider serviceProvider, 
    IOptions<JsonOptions> jsonOptions) : IJobConsumerService
{
    private readonly ILogger<AotJobConsumerService> _logger = logger;
    private readonly IJobStore _jobStore = jobStore;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IOptions<JsonOptions> _jsonOptions = jsonOptions;

    private async Task<MethodResult<string>> ProcessJobPayloadAsync(Job job, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing job {JobId} with name {JobName}", job.Id, job.Name);

        try
        {
            // Get the pre-registered invoker (no runtime reflection)
            var invoker = AotHandlerRegistry.GetInvoker(job.Name);
            if (invoker == null)
            {
                return MethodResult<string>.Failure(new InvalidOperationException($"No invoker registered for job name: {job.Name}"));
            }

            // Get handler registration to know the request type
            var handlerRegistration = HandlerRegistrationTracker.GetByJobName(job.Name);
            if (handlerRegistration == null)
            {
                return MethodResult<string>.Failure(new InvalidOperationException($"Handler registration not found for job name: {job.Name}"));
            }

            // Deserialize the payload to the expected request type
            var request = JsonSerializer.Deserialize(job.Payload, handlerRegistration.RequestType, _jsonOptions.Value.SerializerOptions);
            if (request == null)
            {
                return MethodResult<string>.Failure(new InvalidOperationException($"Failed to deserialize request payload for job: {job.Name}"));
            }

            // Execute the handler using the pre-registered invoker (no runtime reflection)
            var result = await invoker(_serviceProvider, request, cancellationToken);
            
            if (!result.IsSuccess)
            {
                return MethodResult<string>.Failure(result.Error);
            }

            // Serialize the result to string
            var serializedResult = JsonSerializer.Serialize(result.Data, handlerRegistration.ResponseType, _jsonOptions.Value.SerializerOptions);
            return MethodResult<string>.Success(serializedResult);
        }
        catch (Exception ex)
        {
            return MethodResult<string>.Failure(ex);
        }
    }
}
```

### 4. Alternative: Code Generation Approach

For maximum AOT compatibility, a source generator could be used to generate the invoker delegates at compile time:

```csharp
// Source generator would generate specific invoker methods:
[Generator]
public class HandlerInvokerGenerator : ISourceGenerator
{
    public void Execute(GeneratorExecutionContext context)
    {
        // Generate specific invoker classes for each handler type
        // This eliminates all runtime reflection
    }
}
```

### 5. Reflection-Free Job Producer Service

The JobProducerService would be enhanced to validate handler existence at job queuing time:

```csharp
public class AotJobProducerService(
    ILogger<AotJobProducerService> logger, 
    IJobStore jobStore, 
    IOptions<AsyncEndpointsConfigurations> configurations) : IJobProducerService
{
    private readonly ILogger<AotJobProducerService> _logger = logger;
    private readonly IJobStore _jobStore = jobStore;
    private readonly AsyncEndpointsWorkerConfigurations _workerConfigurations = configurations.Value.WorkerConfigurations;
    
    // Cache to track if handlers exist (no runtime reflection during job processing)
    private static readonly ConcurrentDictionary<string, bool> _handlerExistsCache = new();

    public async Task ProduceJobsAsync(ChannelWriter<Job> writerJobChannel, CancellationToken stoppingToken)
    {
        // Implementation would validate handler existence before queuing jobs
        // This prevents jobs from being processed when no handler exists
    }
    
    private static bool HandlerExists(string jobName)
    {
        // Check the static registry (no runtime reflection)
        return AotHandlerRegistry.GetInvoker(jobName) != null;
    }
}
```

## Benefits of the New Architecture

1. **AOT Compatibility**: No runtime reflection, making it compatible with Native AOT scenarios
2. **Performance**: Pre-registered delegates execute faster with no type resolution overhead
3. **Type Safety**: Strong compile-time type checking
4. **Maintainability**: Clear separation between registration and execution

## Migration Path

1. Phase 1: Introduce the new AOT-compatible registration system alongside the existing one
2. Phase 2: Migrate existing handlers to use the new registration methods
3. Phase 3: Deprecate and eventually remove the reflection-based approach

## Conclusion

This architecture provides a path to full AOT compatibility while maintaining the flexibility and functionality of the existing system. The key is to move all type resolution and method invocation logic to application startup time, where reflection can still be used safely, and then execute with pre-registered delegates at runtime.