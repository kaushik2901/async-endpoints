---
sidebar_position: 2
title: Storage Configuration
---

# Storage Configuration

This page explains how to configure storage options for AsyncEndpoints, including in-memory storage for development and Redis storage for production environments.

## Overview

AsyncEndpoints supports multiple storage backends through the `IJobStore` interface. The storage layer is responsible for managing job persistence, state transitions, and job queue operations.

## In-Memory Storage (Development)

### Basic Setup

The in-memory store is perfect for development and single-instance deployments:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsInMemoryStore() // Development storage
    .AddAsyncEndpointsWorker();

var app = builder.Build();
```

### Characteristics
- **Data Persistence**: Volatile - data is lost when the application restarts
- **Concurrency**: Thread-safe within a single application instance
- **Performance**: Fast in-memory operations
- **Suitability**: Development, testing, single-instance deployments
- **Limitations**: No persistence across application restarts, not suitable for multi-instance deployments

### In-Memory Store Implementation

The in-memory store uses concurrent collections for thread safety:

```csharp
public class InMemoryJobStore : IJobStore
{
    private readonly ConcurrentDictionary<Guid, Job> _jobs = new();
    private readonly ConcurrentQueue<Guid> _queue = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    
    // Implementation details for job operations
}
```

## Redis Storage (Production)

### Basic Setup with Connection String

For production environments with persistence and multi-instance support:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsRedisStore("localhost:6379") // Redis connection string
    .AddAsyncEndpointsWorker();

var app = builder.Build();
```

### Setup with Connection Multiplexer

For more advanced scenarios with custom connection configuration:

```csharp
var connection = ConnectionMultiplexer.Connect(new ConfigurationOptions
{
    EndPoints = { "localhost:6379" },
    ConnectRetry = 3,
    ConnectTimeout = 5000,
    AbortOnConnectFail = false
});

builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsRedisStore(connection)
    .AddAsyncEndpointsWorker();
```

### Setup with Configuration Object

For maximum flexibility in configuration:

```csharp
builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsRedisStore(config =>
    {
        config.ConnectionString = "localhost:6379";
        // Additional Redis configuration can be set directly on the ConnectionMultiplexer if needed
    })
    .AddAsyncEndpointsWorker();
```

### Redis Store Characteristics

- **Data Persistence**: Persistent data storage with Redis durability options
- **Concurrency**: Atomic operations for safe multi-instance usage
- **Performance**: Optimized Redis operations with Lua scripts
- **Scalability**: Supports multiple application instances
- **Recovery**: Supports distributed job recovery (when enabled)
- **Monitoring**: Can leverage Redis monitoring and metrics

### Redis Connection Management

The Redis store includes robust connection management:

```csharp
private IDatabase InitializeDatabase(string connectionString)
{
    var redis = ConnectionMultiplexer.Connect(connectionString);

    // Register for connection events to handle reconnection
    redis.ConnectionFailed += (sender, e) =>
        logger.LogError(e.Exception, "Redis connection failed: {ErrorMessage}", e.Exception?.Message);
    redis.ConnectionRestored += (sender, e) =>
        logger.LogInformation("Redis connection restored");

    return redis.GetDatabase();
}
```

## Storage Configuration Options

### Redis Configuration Class

When using the configuration object approach:

```csharp
public class RedisConfiguration
{
    public string ConnectionString { get; set; } = string.Empty;
}
```

## Performance Considerations

### In-Memory Performance
- **Speed**: Very fast operations (microseconds)
- **Memory Usage**: Jobs stored in application memory
- **Scalability**: Limited to single instance
- **Recovery**: No automatic recovery after failures

### Redis Performance
- **Speed**: Fast operations (milliseconds) depending on network and Redis configuration
- **Memory Usage**: Stored in Redis, not application memory
- **Scalability**: Scales with Redis cluster configuration
- **Recovery**: Automatic recovery mechanisms available
- **Latency**: Network latency between application and Redis

## Choosing the Right Storage

### Use In-Memory Storage When:
- Developing or testing applications
- Running single-instance deployments
- Performance is critical and persistence isn't needed
- Data loss on restart is acceptable
- Simple deployment requirements

### Use Redis Storage When:
- Production deployments requiring persistence
- Multi-instance or load-balanced environments
- Need for job recovery mechanisms
- Horizontal scaling requirements
- Data persistence across application restarts
- Complex deployment scenarios

## Migration Between Storage Types

### Development to Production
A common pattern is to use different storage types in different environments:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAsyncEndpoints();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAsyncEndpointsInMemoryStore();
}
else
{
    builder.Services.AddAsyncEndpointsRedisStore(builder.Configuration.GetConnectionString("Redis"));
}

builder.Services.AddAsyncEndpointsWorker();
```

## Monitoring Storage Performance

### In-Memory Metrics
- Monitor application memory usage
- Track queue sizes and processing rates
- Observe job completion times

### Redis Metrics
- Redis server performance metrics
- Connection pool health
- Average response times
- Memory usage on Redis server

## Storage-Specific Configuration Examples

### Production Redis Configuration
```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount;
    options.WorkerConfigurations.MaximumQueueSize = 1000; // Large queue for high throughput
    options.JobManagerConfiguration.DefaultMaxRetries = 5;
});

builder.Services.AddAsyncEndpointsRedisStore(config =>
{
    config.ConnectionString = builder.Configuration.GetConnectionString("Redis");
    config.ConnectRetry = 5;
    config.ConnectTimeout = 10000;
    config.AbortOnConnectFail = false;
});
```

### Development Configuration
```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.WorkerConfigurations.MaximumConcurrency = 2;
    options.WorkerConfigurations.MaximumQueueSize = 50;
    options.WorkerConfigurations.PollingIntervalMs = 1000;
    options.JobManagerConfiguration.DefaultMaxRetries = 1;
});

builder.Services.AddAsyncEndpointsInMemoryStore();
```

## Troubleshooting Storage Issues

### Common In-Memory Issues
- Memory exhaustion due to large queue sizes
- Data loss after application restart
- Thread safety issues (unusual with the built-in implementation)

### Common Redis Issues
- Connection timeouts due to network issues
- Authentication failures
- Redis server resource exhaustion
- Serialization/deserialization problems

The storage configuration system provides flexibility to choose the right persistence strategy for your deployment requirements while maintaining consistent API behavior across storage implementations.