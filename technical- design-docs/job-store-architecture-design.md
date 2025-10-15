# Job Store Architecture Design

## Overview

This document outlines the architecture for implementing multiple job store support in AsyncEndpoints, with proper separation of concerns between data storage and job lifecycle management.

## Current Architecture Issues

### Responsibilities Confusion
Currently, the `IJobStore` interface and `InMemoryJobStore` implementation have mixed responsibilities:
- **Data abstraction**: Storing and retrieving job data (valid responsibility)
- **Job lifecycle management**: Handling retries, claiming jobs for workers, scheduling (invalid responsibility)

### Problems with Current Implementation
1. **Tight Coupling**: The job store is responsible for business logic (retry handling)
2. **Scalability Issues**: Worker claiming logic in the store doesn't scale well across multiple instances
3. **Extension Difficulty**: Adding new job stores requires implementing business logic again
4. **Performance Bottleneck**: All job lifecycle logic runs through the data store

## Proposed Architecture

### Separation of Concerns

We propose splitting responsibilities between two distinct components:

#### 1. JobStore (Data Abstraction Layer)
- **Purpose**: Provide abstraction over underlying data storage
- **Responsibilities**:
  - Create, read, update, delete job records
  - Query operations (get by ID, get by status, etc.)
  - Transaction management (where applicable)
  - Connection management
- **Implementation Examples**: RedisJobStore, EFCoreJobStore, InMemoryJobStore

#### 2. JobManager (Job Lifecycle Management)
- **Purpose**: Manage job lifecycle, retries, worker assignment
- **Responsibilities**:
  - Claim jobs for workers
  - Handle retry logic and backoff strategies
  - Schedule delayed/retried jobs
  - Process job completion/failure workflows
  - Coordinate worker activities
- **Implementation**: Single implementation that works with any JobStore

### Component Diagram

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Background    │    │   JobManager    │    │   JobStore      │
│   Service       │───▶│   (Service)     │───▶│   (Interface)   │
│                 │    │                 │    │                 │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                            │                        │
                            ▼                        ▼
                    ┌─────────────────┐    ┌─────────────────┐
                    │ Job Processing  │    │ Data Operations │
                    │ Logic           │    │ (CRUD)          │
                    └─────────────────┘    └─────────────────┘
                         (Business)             (Data Access)
```

## Interface Definitions

### Updated IJobStore Interface

```csharp
public interface IJobStore
{
    /// <summary>
    /// Creates a new job in the store
    /// </summary>
    Task<MethodResult> CreateJob(Job job, CancellationToken cancellationToken);
    
    /// <summary>
    /// Retrieves a job by its unique identifier
    /// </summary>
    Task<MethodResult<Job?>> GetJobById(Guid id, CancellationToken cancellationToken);
    
    /// <summary>
    /// Updates the complete job entity
    /// </summary>
    Task<MethodResult> UpdateJob(Job job, CancellationToken cancellationToken);
    
    /// <summary>
    /// Updates only the job status
    /// </summary>
    Task<MethodResult> UpdateJobStatus(Guid jobId, JobStatus status, CancellationToken cancellationToken);
    
    /// <summary>
    /// Updates job result
    /// </summary>
    Task<MethodResult> UpdateJobResult(Guid jobId, string result, CancellationToken cancellationToken);
    
    /// <summary>
    /// Updates job exception details
    /// </summary>
    Task<MethodResult> UpdateJobException(Guid jobId, string exception, CancellationToken cancellationToken);
    
    /// <summary>
    /// Atomically claims available jobs for a specific worker
    /// </summary>
    Task<MethodResult<List<Job>>> ClaimJobsForWorker(Guid workerId, int maxClaimCount, CancellationToken cancellationToken);
    
    /// <summary>
    /// Gets jobs by status
    /// </summary>
    Task<MethodResult<List<Job>>> GetJobsByStatus(JobStatus status, int limit, CancellationToken cancellationToken);
    
    /// <summary>
    /// Gets scheduled jobs that are due for processing
    /// </summary>
    Task<MethodResult<List<Job>>> GetDueScheduledJobs(int limit, CancellationToken cancellationToken);
    
    /// <summary>
    /// Releases a job back to the pool (for failed workers/timeout)
    /// </summary>
    Task<MethodResult> ReleaseJobToPool(Guid jobId, CancellationToken cancellationToken);
    
    /// <summary>
    /// Deletes a job from the store
    /// </summary>
    Task<MethodResult> DeleteJob(Guid jobId, CancellationToken cancellationToken);
    
    /// <summary>
    /// Gets statistics about jobs in the system
    /// </summary>
    Task<MethodResult<JobStatistics>> GetJobStatistics(CancellationToken cancellationToken);
    
    /// <summary>
    /// Cleans up completed jobs older than the specified age
    /// </summary>
    Task<MethodResult<int>> CleanupCompletedJobs(TimeSpan maxAge, CancellationToken cancellationToken);
}

public class JobStatistics
{
    public int QueuedCount { get; set; }
    public int InProgressCount { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public int ScheduledCount { get; set; }
    public int CanceledCount { get; set; }
}
```

### IJobManager Interface

```csharp
public interface IJobManager
{
    /// <summary>
    /// Submits a new job to the system
    /// </summary>
    Task<MethodResult<Guid>> SubmitJob(Job job, CancellationToken cancellationToken);
    
    /// <summary>
    /// Claims available jobs for processing by a worker
    /// </summary>
    Task<MethodResult<List<Job>>> ClaimJobsForProcessing(Guid workerId, int maxClaimCount, CancellationToken cancellationToken);
    
    /// <summary>
    /// Processes a successful job completion
    /// </summary>
    Task<MethodResult> ProcessJobSuccess(Guid jobId, string result, CancellationToken cancellationToken);
    
    /// <summary>
    /// Processes a failed job (with potential retry logic)
    /// </summary>
    Task<MethodResult> ProcessJobFailure(Guid jobId, string exception, CancellationToken cancellationToken);
    
    /// <summary>
    /// Processes a job cancellation
    /// </summary>
    Task<MethodResult> ProcessJobCancellation(Guid jobId, CancellationToken cancellationToken);
    
    /// <summary>
    /// Checks and schedules any due scheduled/retry jobs
    /// </summary>
    Task<MethodResult> ProcessScheduledJobs(CancellationToken cancellationToken);
    
    /// <summary>
    /// Monitors and releases jobs that have been claimed too long (timeout)
    /// </summary>
    Task<MethodResult> ProcessStaleJobClaims(CancellationToken cancellationToken);
    
    /// <summary>
    /// Cancels a job if it's not yet started
    /// </summary>
    Task<MethodResult> CancelJob(Guid jobId, CancellationToken cancellationToken);
    
    /// <summary>
    /// Gets a job with full details
    /// </summary>
    Task<MethodResult<Job?>> GetJobById(Guid jobId, CancellationToken cancellationToken);
    
    /// <summary>
    /// Gets job statistics from the underlying store
    /// </summary>
    Task<MethodResult<JobStatistics>> GetJobStatistics(CancellationToken cancellationToken);
}
```

## Implementation Patterns

### JobStore Implementations

#### RedisJobStore
- Uses Redis for distributed job storage
- Leverages Redis atomic operations for job claiming
- Uses Redis sorted sets for scheduling

#### EFCoreJobStore
- Uses EF Core with database transactions
- Uses database locking mechanisms for job claiming
- Leverages database queries for scheduling

#### InMemoryJobStore
- Uses concurrent collections for thread-safe operations
- Simple in-memory locking for job claiming
- Timer-based scheduling

### JobManager Implementation

The JobManager will be a single implementation that orchestrates the job lifecycle:

```csharp
public class DefaultJobManager(IJobStore jobStore, ILogger<DefaultJobManager> logger, IOptions<JobManagerConfiguration> options) : IJobManager
{
    public async Task<MethodResult<Guid>> SubmitJob(Job job, CancellationToken cancellationToken)
    {
        // 1. Set initial job properties
        job.Status = JobStatus.Queued;
        job.CreatedAt = DateTimeOffset.UtcNow;
        job.LastUpdatedAt = DateTimeOffset.UtcNow;
        
        // 2. Persist the job
        var result = await jobStore.CreateJob(job, cancellationToken);
        return result.IsSuccess ? MethodResult<Guid>.Success(job.Id) : MethodResult<Guid>.Failure(result.Error);
    }
    
    public async Task<MethodResult<List<Job>>> ClaimJobsForProcessing(Guid workerId, int maxClaimCount, CancellationToken cancellationToken)
    {
        // 1. Get available jobs (queued, scheduled, etc.)
        var availableJobs = await jobStore.ClaimJobsForWorker(workerId, maxClaimCount, cancellationToken);
        
        // 2. Update job status to InProgress and assign to worker
        if (availableJobs.IsSuccess && availableJobs.Data?.Any() == true)
        {
            foreach (var job in availableJobs.Data)
            {
                job.UpdateStatus(JobStatus.InProgress);
                job.WorkerId = workerId;
                await jobStore.UpdateJob(job, cancellationToken);
            }
        }
        
        return availableJobs;
    }
    
    public async Task<MethodResult> ProcessJobFailure(Guid jobId, string exception, CancellationToken cancellationToken)
    {
        // 1. Get current job state
        var jobResult = await jobStore.GetJobById(jobId, cancellationToken);
        if (!jobResult.IsSuccess || jobResult.Data == null)
            return MethodResult.Failure(new AsyncEndpointError("JOB_NOT_FOUND", $"Job {jobId} not found"));
            
        var job = jobResult.Data;
        
        // 2. Check if retry is possible
        if (job.RetryCount < job.MaxRetries)
        {
            // Increment retry count and schedule retry
            job.IncrementRetryCount();
            var retryDelay = CalculateRetryDelay(job.RetryCount);
            job.SetRetryTime(DateTimeOffset.UtcNow.Add(retryDelay));
            job.UpdateStatus(JobStatus.Scheduled);
            job.WorkerId = null; // Release from current worker
            job.Exception = exception;
        }
        else
        {
            // Mark as failed permanently
            job.SetException(exception);
        }
        
        // 3. Persist updated job state
        return await jobStore.UpdateJob(job, cancellationToken);
    }
    
    private TimeSpan CalculateRetryDelay(int retryCount)
    {
        // Exponential backoff: 2^retryCount * base delay
        return TimeSpan.FromSeconds(Math.Pow(2, retryCount) * options.Value.RetryDelayBaseSeconds);
    }
}
```

## Separation of Concerns

### JobStore Responsibilities (Data Abstraction)
The JobStore is responsible for data-related operations only:

- **Data Persistence**: Store and retrieve job data from the underlying storage system
- **Atomic Operations**: Ensure data consistency through atomic operations where possible
- **Distributed Locking**: Implement distributed locking mechanisms appropriate for the storage system
- **Connection Management**: Handle connections, transactions, and resource management for the storage system
- **Querying**: Provide methods to retrieve jobs based on various criteria
- **Indexing**: Implement appropriate indexing strategies for efficient queries

#### What JobStore Should NOT Do:
- Job lifecycle management
- Retry logic
- Worker assignment decisions
- Scheduling business logic
- Job processing orchestration

### JobManager Responsibilities (Business Logic)
The JobManager handles the job lifecycle and business logic:

- **Job Lifecycle**: Manage the complete lifecycle from creation to completion/failure
- **Retry Logic**: Implement retry strategies, backoff algorithms, and failure handling
- **Worker Assignment**: Determine which jobs get assigned to which workers
- **Scheduling**: Handle delayed jobs and scheduled job execution
- **Monitoring**: Track job progress, detect stale jobs, handle timeouts
- **Coordination**: Coordinate between multiple workers in distributed scenarios

#### What JobManager Should NOT Do:
- Direct data access implementation details
- Storage-specific optimization
- Database-specific transaction management

### Distributed Locking Implementation

#### How the Architecture Enables Distributed Locking

The separation of concerns in this design allows each JobStore implementation to use the most appropriate distributed locking mechanism for its underlying storage system:

**RedisJobStore Implementation:**
- Uses Redis atomic operations (LPOP, LPUSH) for job claiming
- Leverages Redis locks (SET key NX EX) for distributed synchronization
- Uses Redis sorted sets for scheduling with atomic operations

**EFCoreJobStore Implementation:**
- Uses database transactions with row-level locking (SELECT ... FOR UPDATE)
- Implements optimistic locking with version fields
- Uses database-specific locking mechanisms (SQL Server row locks, PostgreSQL advisory locks)

**InMemoryJobStore Implementation:**
- Uses .NET concurrent collections and SemaphoreSlim for in-process synchronization
- Provides thread-safe operations without network overhead

#### Distributed Locking Methods in IJobStore

The `ClaimJobsForWorker` method in IJobStore is designed to be atomic and provide distributed locking:

```csharp
/// <summary>
/// Atomically claims available jobs for a specific worker
/// Implements distributed locking appropriate for the underlying storage
/// </summary>
Task<MethodResult<List<Job>>> ClaimJobsForWorker(Guid workerId, int maxClaimCount, CancellationToken cancellationToken);
```

This method ensures that:
1. Only one worker can claim a specific job at a time
2. The operation is atomic to prevent race conditions
3. Each JobStore implementation can use the most efficient locking mechanism for its storage system

#### Example Implementation Pattern

**RedisJobStore:**
```csharp
public async Task<MethodResult<List<Job>>> ClaimJobsForWorker(Guid workerId, int maxClaimCount, CancellationToken cancellationToken)
{
    // Use Redis Lua script for atomic job claiming
    var luaScript = @"
        local jobs = redis.call('ZRANGEBYSCORE', KEYS[1], '-inf', ARGV[1], 'LIMIT', 0, ARGV[2])
        if #jobs > 0 then
            for _, jobId in ipairs(jobs) do
                redis.call('HSET', ARGV[3] .. jobId, 'workerId', ARGV[4])
                redis.call('HSET', ARGV[3] .. jobId, 'status', 'InProgress')
            end
        end
        return jobs
    ";
    
    var result = await _redisDb.ScriptEvaluateAsync(luaScript, 
        new RedisKey[] { "scheduled_jobs" },
        new RedisValue[] { DateTimeOffset.UtcNow.ToUnixTimeSeconds(), maxClaimCount, "job:", workerId.ToString() });
    
    // Process result...
}
```

**EFCoreJobStore:**
```csharp
public async Task<MethodResult<List<Job>>> ClaimJobsForWorker(Guid workerId, int maxClaimCount, CancellationToken cancellationToken)
{
    using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
    try
    {
        var availableJobs = await _context.Jobs
            .Where(j => j.WorkerId == null && 
                       (j.Status == JobStatus.Queued || 
                        (j.Status == JobStatus.Scheduled && j.RetryDelayUntil <= DateTime.UtcNow)))
            .OrderBy(j => j.CreatedAt)
            .Take(maxClaimCount)
            .Select(j => j.Id)
            .ToListAsync(cancellationToken);
        
        // Use pessimistic locking to lock the selected rows
        var jobsToClaim = await _context.Jobs
            .Where(j => availableJobs.Contains(j.Id))
            .AsTracking()
            .ToListAsync(cancellationToken);
        
        foreach (var job in jobsToClaim)
        {
            job.WorkerId = workerId;
            job.UpdateStatus(JobStatus.InProgress);
        }
        
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        
        return MethodResult<List<Job>>.Success(jobsToClaim);
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync(cancellationToken);
        throw;
    }
}
```

#### Benefits of This Approach

1. **Implementation-Specific Optimizations**: Each job store can use the most efficient locking mechanism for its storage system
2. **Distributed Safety**: Proper distributed locking prevents multiple workers from processing the same job
3. **Performance**: Each implementation can optimize for its specific storage system's capabilities
4. **Scalability**: The locking mechanism scales with the underlying storage system
5. **Flexibility**: New storage systems can be added with their own optimized locking strategies

## Performance Considerations

### Scalability
- **Distributed Locking**: Use atomic operations in Redis/Database to avoid race conditions when claiming jobs
- **Connection Pooling**: Proper connection management in each JobStore implementation
- **Batch Operations**: Support for batch operations where possible to reduce network calls

### Transaction Management
- **Atomic Operations**: Ensure job status updates are atomic to prevent inconsistent states
- **Optimistic/Pessimistic Locking**: Choose appropriate locking strategy based on the underlying store

### Caching
- **Job Caching**: Consider caching frequently accessed jobs in memory (with proper invalidation)
- **Connection Caching**: Maintain connection pools for better performance

## Extension Model

### Registering Job Stores
```csharp
public static IServiceCollection AddRedisJobStore(this IServiceCollection services, Action<RedisJobStoreOptions> configureOptions)
{
    services.Configure(configureOptions);
    services.AddScoped<IJobStore, RedisJobStore>();
    services.AddScoped<IJobManager, DefaultJobManager>();
    return services;
}

public static IServiceCollection AddEFCoreJobStore(this IServiceCollection services, Action<EFCoreJobStoreOptions> configureOptions)
{
    services.Configure(configureOptions);
    services.AddScoped<IJobStore, EFCoreJobStore>();
    services.AddScoped<IJobManager, DefaultJobManager>();
    return services;
}

public static IServiceCollection AddInMemoryJobStore(this IServiceCollection services)
{
    services.AddScoped<IJobStore, InMemoryJobStore>();
    services.AddScoped<IJobManager, DefaultJobManager>();
    return services;
}
```

### Configuration
```csharp
public class JobManagerConfiguration
{
    public int DefaultMaxRetries { get; set; } = 3;
    public double RetryDelayBaseSeconds { get; set; } = 2.0;
    public TimeSpan JobClaimTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxConcurrentJobs { get; set; } = 10;
    public int JobPollingIntervalMs { get; set; } = 1000;
    public int MaxClaimBatchSize { get; set; } = 10;
    public TimeSpan StaleJobClaimCheckInterval { get; set; } = TimeSpan.FromMinutes(1);
}

public class RedisJobStoreOptions
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public string JobsKeyPrefix { get; set; } = "async_jobs:";
    public int RetryCount { get; set; } = 3;
}

public class EFCoreJobStoreOptions
{
    public string ConnectionString { get; set; } = "";
    public string TableName { get; set; } = "AsyncJobs";
    public bool AutoCreateTables { get; set; } = true;
}
```

### Extensibility Points

#### Custom Retry Strategies
```csharp
public interface IRetryStrategy
{
    TimeSpan CalculateDelay(int retryCount, Job job);
    bool ShouldRetry(int retryCount, Job job, Exception? exception);
}

public class ExponentialBackoffRetryStrategy : IRetryStrategy
{
    public TimeSpan CalculateDelay(int retryCount, Job job)
    {
        return TimeSpan.FromSeconds(Math.Pow(2, retryCount) * 2.0); // 2s, 4s, 8s, etc.
    }

    public bool ShouldRetry(int retryCount, Job job, Exception? exception)
    {
        return retryCount < job.MaxRetries;
    }
}
```

#### Custom Scheduling Strategies
```csharp
public interface ISchedulingStrategy
{
    Task<MethodResult<List<Job>>> GetDueJobs(IJobStore jobStore, int maxCount, CancellationToken cancellationToken);
}

public class TimedSchedulingStrategy : ISchedulingStrategy
{
    public async Task<MethodResult<List<Job>>> GetDueJobs(IJobStore jobStore, int maxCount, CancellationToken cancellationToken)
    {
        return await jobStore.GetDueScheduledJobs(maxCount, cancellationToken);
    }
}
```

#### Pluggable Job Stores
The architecture supports any type of job store implementation as long as it implements `IJobStore`:

- **Redis**: For high-performance, distributed scenarios
- **Entity Framework Core**: For SQL database persistence  
- **MongoDB**: For document-based storage
- **Azure Queue Storage**: For cloud-native scenarios
- **Custom implementations**: For specialized requirements

#### Performance Optimization Strategies
1. **Connection Pooling**: Each JobStore implementation manages its own connections
2. **Batch Operations**: Support for processing multiple jobs in single operations
3. **Caching**: Optional caching layer between JobManager and JobStore
4. **Partitioning**: Ability to partition jobs across multiple stores based on criteria

## Implementation Guidelines

### Best Practices for JobStore Implementations
1. **Thread Safety**: Ensure all operations are thread-safe
2. **Atomicity**: Use atomic operations for critical sections (claiming jobs, updating status)
3. **Error Handling**: Implement proper error handling and retry mechanisms
4. **Resource Management**: Properly dispose of connections and resources
5. **Performance**: Optimize for high-throughput scenarios

### Best Practices for JobManager Implementation
1. **State Management**: Properly manage and validate job states during transitions
2. **Race Condition Prevention**: Use proper synchronization mechanisms
3. **Monitoring**: Provide metrics and logging for operational visibility
4. **Graceful Degradation**: Handle partial failures gracefully
5. **Scalability**: Design for horizontal scaling across multiple instances

### Migration Path for Existing Users
1. **Backward Compatibility**: Maintain existing public APIs during transition
2. **Incremental Migration**: Allow gradual migration from old to new architecture
3. **Configuration Migration**: Provide automatic configuration mapping
4. **Testing**: Comprehensive testing to ensure no regressions

## Migration Path

### From Current Implementation
1. **Phase 1**: Introduce new interfaces alongside existing ones
2. **Phase 2**: Refactor InMemoryJobStore to only handle data operations
3. **Phase 3**: Implement JobManager with current business logic
4. **Phase 4**: Update Background services to use JobManager
5. **Phase 5**: Implement Redis/EF Core job stores

### Backward Compatibility
- Maintain existing API surface where possible
- Provide migration guides for existing users

## Security Considerations

### Job Data Security
- Ensure job payloads are properly sanitized
- Implement proper authentication/authorization for job access
- Encrypt sensitive job data if needed

### Worker Authentication
- Validate worker identities when claiming jobs
- Implement job ownership validation

## Testing Strategy

### Unit Tests
- Test JobStore implementations in isolation
- Test JobManager logic with mocked JobStore
- Test edge cases (concurrent access, failures, timeouts)

### Integration Tests
- End-to-end job processing workflows
- Distributed scenarios with multiple workers
- Different job store implementations

### Performance Tests
- Throughput under load
- Latency for job processing
- Concurrency handling