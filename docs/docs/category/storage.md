---
sidebar_position: 10
---

# Storage Options

## Overview

AsyncEndpoints provides multiple storage options to handle job persistence and coordination. The storage backend determines how jobs are stored, retrieved, and processed in your application.

## Available Storage Options

### In-Memory Store

The in-memory store is designed for development and single-instance deployments.

**Pros:**
- Simple setup with no external dependencies
- Fast performance for development
- Ideal for testing and prototyping

**Cons:**
- No persistence across application restarts
- Not suitable for multi-instance deployments
- Limited scalability

**Usage:**
```csharp
builder.Services.AddAsyncEndpointsInMemoryStore();
```

**Best For:**
- Development environments
- Single-instance applications
- Testing and prototyping
- Applications where job persistence isn't critical

### Redis Store

The Redis store provides production-ready, distributed job storage.

**Pros:**
- Persistent storage
- Supports multi-instance deployments
- Distributed job coordination
- Automatic job recovery
- Scalable architecture
- High performance
- Built-in job retry and scheduling

**Cons:**
- Requires Redis infrastructure
- Slightly more complex setup
- Additional operational overhead

**Usage:**
```csharp
// Using connection string
builder.Services.AddAsyncEndpointsRedisStore("localhost:6379");

// Using connection multiplexer
var connection = ConnectionMultiplexer.Connect("localhost:6379");
builder.Services.AddAsyncEndpointsRedisStore(connection);

// Using configuration object
builder.Services.AddAsyncEndpointsRedisStore(config =>
{
    config.ConnectionString = "localhost:6379";
});
```

**Best For:**
- Production environments
- Multi-instance deployments
- Applications requiring job persistence
- Distributed systems
- Applications with high availability requirements

## Storage Architecture

### In-Memory Implementation

The in-memory store uses thread-safe collections to maintain job state:

- `ConcurrentDictionary<Guid, Job>` for job storage
- `ConcurrentQueue<Guid>` for job queuing
- `SemaphoreSlim` for thread coordination
- In-memory locking for job claiming

### Redis Implementation

The Redis store uses multiple data structures for optimal performance:

- **Job Storage**: Hashes (`HSET`) to store job details
- **Job Queue**: Sorted sets (`ZADD`) with timestamps as scores for prioritization
- **In-Progress Tracking**: Sorted sets for monitoring active jobs
- **Lua Scripts**: To ensure atomic operations during job claiming
- **Connection Multiplexing**: For efficient Redis connection management

## Redis Store Configuration

### Connection Options

You can configure Redis connections in multiple ways:

#### Connection String
```csharp
builder.Services.AddAsyncEndpointsRedisStore("localhost:6379,password=yourpassword,ssl=true");
```

#### Connection Multiplexer
```csharp
var options = ConfigurationOptions.Parse("localhost:6379");
options.Password = "yourpassword";
options.Ssl = true;

var connection = ConnectionMultiplexer.Connect(options);
builder.Services.AddAsyncEndpointsRedisStore(connection);
```

#### Configuration Action
```csharp
builder.Services.AddAsyncEndpointsRedisStore(config =>
{
    config.ConnectionString = "localhost:6379,password=yourpassword,ssl=true";
});
```

### Redis Key Structure

The Redis store uses the following key patterns:

- `ae:job:{jobId}`: Individual job hash containing all job details
- `ae:jobs:queue`: Sorted set of job IDs queued for processing
- `ae:jobs:inprogress`: Sorted set of job IDs currently being processed

### Redis Lua Scripts

The Redis implementation uses Lua scripts to ensure atomic operations during job claiming, which prevents race conditions when multiple workers try to claim the same job simultaneously.

## Job Recovery

### Distributed Recovery

In multi-instance deployments, the Redis store supports automatic job recovery:

- **Stuck Job Detection**: Monitors for jobs claimed longer than the configured timeout
- **Automatic Recovery**: Returns stuck jobs to the queue for reprocessing
- **Retry Logic**: Applies retry logic to recovered jobs if retry attempts remain

Configure recovery behavior:
```csharp
builder.Services.AddAsyncEndpointsWorker(recoveryConfiguration =>
{
    recoveryConfiguration.EnableDistributedJobRecovery = true;  // Default: true
    recoveryConfiguration.JobTimeoutMinutes = 30;               // Time before job considered stuck
    recoveryConfiguration.RecoveryCheckIntervalSeconds = 300;   // How often to check for stuck jobs
    recoveryConfiguration.MaximumRetries = 3;                   // Additional retries for recovered jobs
});
```

## Performance Considerations

### In-Memory Store Performance

- **Memory Usage**: Jobs consume application memory; consider memory limits
- **Scalability**: Limited to single instance; no horizontal scaling
- **Restart Impact**: All jobs lost on application restart

### Redis Store Performance

- **Connection Management**: Uses connection multiplexing for efficient Redis communication
- **Network Latency**: Consider network latency when using remote Redis instances
- **Serialization**: Jobs are JSON serialized/deserialized for storage
- **Key Expiration**: Consider implementing key expiration strategies for cleanup

## Custom Storage Implementation

You can implement custom storage by implementing the `IJobStore` interface:

```csharp
public interface IJobStore
{
    bool SupportsJobRecovery { get; }
    
    Task<MethodResult> CreateJob(Job job, CancellationToken cancellationToken);
    Task<MethodResult<Job>> GetJobById(Guid id, CancellationToken cancellationToken);
    Task<MethodResult> UpdateJob(Job job, CancellationToken cancellationToken);
    Task<MethodResult<Job>> ClaimNextJobForWorker(Guid workerId, CancellationToken cancellationToken);
}
```

### Example: Custom Database Storage

```csharp
public class DatabaseJobStore : IJobStore
{
    private readonly IDbConnection _connection;
    
    public bool SupportsJobRecovery => true;
    
    public async Task<MethodResult> CreateJob(Job job, CancellationToken cancellationToken)
    {
        try
        {
            var sql = @"
                INSERT INTO Jobs (Id, Name, Status, Payload, CreatedAt, LastUpdatedAt) 
                VALUES (@Id, @Name, @Status, @Payload, @CreatedAt, @LastUpdatedAt)";
                
            await _connection.ExecuteAsync(sql, job, cancellationToken: cancellationToken);
            return MethodResult.Success();
        }
        catch (Exception ex)
        {
            return MethodResult.Failure(ex);
        }
    }
    
    public async Task<MethodResult<Job>> GetJobById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var sql = "SELECT * FROM Jobs WHERE Id = @Id";
            var job = await _connection.QueryFirstOrDefaultAsync<Job>(sql, new { Id = id }, cancellationToken: cancellationToken);
            
            return job != null 
                ? MethodResult<Job>.Success(job) 
                : MethodResult<Job>.Failure("Job not found");
        }
        catch (Exception ex)
        {
            return MethodResult<Job>.Failure(ex);
        }
    }
    
    // Implement other methods...
}
```

## Migration Strategies

### From In-Memory to Redis

1. **Implement Redis Store**: Add Redis store configuration
2. **Data Migration**: Implement migration logic for existing jobs if needed
3. **Testing**: Verify job processing in Redis environment
4. **Switch Over**: Update production configuration to use Redis

### From Redis to Custom Storage

1. **Implement Custom Store**: Create your custom `IJobStore` implementation
2. **Data Migration**: Plan and execute data migration from Redis
3. **Validation**: Ensure all job state transitions work correctly
4. **Testing**: Test retry logic, recovery, and error handling

## Best Practices

### For In-Memory Store
- Use only in development or testing environments
- Monitor memory usage closely
- Don't rely on job persistence across application restarts
- Set appropriate queue size limits to prevent memory exhaustion

### For Redis Store
- Monitor Redis memory usage and performance
- Configure appropriate connection timeouts
- Implement Redis cluster for high availability if needed
- Regularly backup Redis data if required
- Monitor connection pool metrics
- Configure appropriate expiration policies

### General Storage Best Practices
- Choose storage based on your application requirements (persistence, scaling, etc.)
- Monitor storage performance metrics
- Implement proper error handling for storage failures
- Test storage failover scenarios
- Plan for data growth and cleanup strategies
- Implement appropriate security for storage systems

## Configuration Example

Here's a complete example showing Redis storage configuration:

```csharp
using AsyncEndpoints.Extensions;
using AsyncEndpoints.Redis.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Redis configuration from appsettings.json
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");

builder.Services
    .AddAsyncEndpoints(options =>
    {
        // Configure worker settings
        options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount;
        options.WorkerConfigurations.MaximumQueueSize = 100;
        
        // Configure job settings
        options.JobManagerConfiguration.DefaultMaxRetries = 3;
        options.JobManagerConfiguration.RetryDelayBaseSeconds = 3.0;
    })
    .AddAsyncEndpointsRedisStore(redisConnectionString)
    .AddAsyncEndpointsWorker(recoveryConfiguration =>
    {
        recoveryConfiguration.EnableDistributedJobRecovery = true;
        recoveryConfiguration.JobTimeoutMinutes = 30;
    });

var app = builder.Build();

app.MapAsyncGetJobDetails("/jobs/{jobId:guid}");
app.MapAsyncPost<DataRequest>("ProcessData", "/api/process-data");

await app.RunAsync();
```

Choose the storage option that best fits your application's requirements for persistence, scalability, and deployment architecture.