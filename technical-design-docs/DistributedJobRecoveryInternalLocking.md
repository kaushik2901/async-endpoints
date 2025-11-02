# Technical Design Document: Distributed Job Recovery Service Race Condition with Internal Redis Locking

## Overview

The `DistributedJobRecoveryService` currently runs on every service instance where `AddAsyncEndpointsWorker` is called with recovery configuration enabled. This creates a race condition where multiple instances simultaneously attempt to recover stuck jobs, leading to redundant processing and potential performance degradation.

This document proposes a solution using an internal, separate distributed locking service that is used only within the `RedisJobStore` implementation.

## Problem Statement

When multiple instances of the service are deployed and the `DistributedJobRecoveryService` is enabled, all instances execute the same recovery logic simultaneously:

```csharp
private async Task RecoverStuckJobs(CancellationToken cancellationToken)
{
    var timeoutUnixTime = _dateTimeProvider.DateTimeOffsetNow.AddMinutes(-_jobTimeoutMinutes).ToUnixTimeSeconds();
    var recoveredCount = await _jobStore.RecoverStuckJobs(
        timeoutUnixTime,
        _maxRetries,
        cancellationToken);

    if (recoveredCount > 0)
    {
        _logger.LogInformation("Recovered {RecoveredCount} stuck jobs", recoveredCount);
    }
    else
    {
        _logger.LogDebug("No stuck jobs found during recovery check");
    }
}
```

Each instance calls `_jobStore.RecoverStuckJobs()` at the same interval, potentially causing:
- Multiple services attempting to recover the same stuck jobs
- Redundant execution of recovery operations
- Increased load on the job store
- Inefficient resource utilization

## Solution: Internal Distributed Locking in Redis Job Store

### Proposed Approach

Create a separate `IDistributedLockService` that is used only internally within the `RedisJobStore` implementation. This keeps the locking concerns internal to the Redis implementation and doesn't expose them through the `IJobStore` interface.

### Interface Definition

```csharp
/// <summary>
/// Provides distributed locking capabilities across multiple service instances.
/// </summary>
public interface IDistributedLockService
{
    /// <summary>
    /// Attempts to acquire a distributed lock with the specified key.
    /// </summary>
    /// <param name="lockKey">The unique key for the lock</param>
    /// <param name="lockValue">The value to store with the lock (should be unique per caller)</param>
    /// <param name="timeout">The duration after which the lock automatically expires</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the lock was acquired, false otherwise</returns>
    Task<bool> TryAcquireLockAsync(string lockKey, string lockValue, TimeSpan timeout, CancellationToken cancellationToken);
    
    /// <summary>
    /// Releases a previously acquired distributed lock.
    /// </summary>
    /// <param name="lockKey">The key of the lock to release</param>
    /// <param name="lockValue">The value that was used to acquire the lock</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the lock was released, false if it was not held by the caller</returns>
    Task<bool> TryReleaseLockAsync(string lockKey, string lockValue, CancellationToken cancellationToken);
}
```

### Redis Implementation

```csharp
/// <summary>
/// Redis-based implementation of IDistributedLockService using SET NX EX commands for acquiring
/// and Lua script for safe releasing.
/// </summary>
public class RedisDistributedLockService : IDistributedLockService
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisDistributedLockService> _logger;

    public RedisDistributedLockService(IDatabase database, ILogger<RedisDistributedLockService> logger)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> TryAcquireLockAsync(string lockKey, string lockValue, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(lockKey)) throw new ArgumentException("Lock key cannot be null or empty", nameof(lockKey));
        if (string.IsNullOrEmpty(lockValue)) throw new ArgumentException("Lock value cannot be null or empty", nameof(lockValue));

        try
        {
            // Use Redis SET with NX (Not eXists) and EX (expiration) options
            var acquired = await _database.StringSetAsync(
                lockKey,
                lockValue,
                timeout,
                When.NotExists,
                CommandFlags.HighPriority);

            if (acquired)
            {
                _logger.LogDebug("Distributed lock acquired: {LockKey}", lockKey);
            }
            else
            {
                _logger.LogDebug("Failed to acquire distributed lock: {LockKey}", lockKey);
            }

            return acquired;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acquiring distributed lock: {LockKey}", lockKey);
            throw;
        }
    }

    public async Task<bool> TryReleaseLockAsync(string lockKey, string lockValue, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(lockKey)) throw new ArgumentException("Lock key cannot be null or empty", nameof(lockKey));
        if (string.IsNullOrEmpty(lockValue)) throw new ArgumentException("Lock value cannot be null or empty", nameof(lockValue));

        try
        {
            // Lua script to safely delete the key only if value matches (prevents deleting another instance's lock)
            var luaScript = @"
                if redis.call('GET', KEYS[1]) == ARGV[1] then
                    return redis.call('DEL', KEYS[1])
                else
                    return 0
                end";
            
            var result = await _database.ScriptEvaluateAsync(
                luaScript, 
                new RedisKey[] { lockKey }, 
                new RedisValue[] { lockValue });

            var released = (int)result > 0;
            
            if (released)
            {
                _logger.LogDebug("Distributed lock released: {LockKey}", lockKey);
            }
            else
            {
                _logger.LogDebug("Failed to release distributed lock (not held by caller): {LockKey}", lockKey);
            }

            return released;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing distributed lock: {LockKey}", lockKey);
            throw;
        }
    }
}
```

### Updated Redis Job Store Implementation

The `RedisJobStore` will be updated to use the distributed lock service internally for recovery operations:

```csharp
/// <inheritdoc />
public class RedisJobStore : IJobStore
{
    private readonly ILogger<RedisJobStore> _logger;
    private readonly IDatabase _database;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IJobHashConverter _jobHashConverter;
    private readonly ISerializer _serializer;
    private readonly IRedisLuaScriptService _redisLuaScriptService;
    private readonly IAsyncEndpointsObservability _metrics;
    private readonly IDistributedLockService? _distributedLockService; // Internal lock service

    // Updated constructor to accept the lock service
    public RedisJobStore(
        ILogger<RedisJobStore> logger, 
        IDatabase database, 
        IDateTimeProvider dateTimeProvider, 
        IJobHashConverter jobHashConverter, 
        ISerializer serializer, 
        IRedisLuaScriptService redisLuaScriptService, 
        IAsyncEndpointsObservability metrics,
        IDistributedLockService? distributedLockService = null) // Optional for backward compatibility
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        _jobHashConverter = jobHashConverter ?? throw new ArgumentNullException(nameof(jobHashConverter));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _redisLuaScriptService = redisLuaScriptService ?? throw new ArgumentNullException(nameof(redisLuaScriptService));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _distributedLockService = distributedLockService;
    }

    // Existing interface implementation remains the same...
    public bool SupportsJobRecovery => true;

    /// <inheritdoc />
    public async Task<int> RecoverStuckJobs(long timeoutUnixTime, int maxRetries, CancellationToken cancellationToken)
    {
        // If no distributed lock service is available, run recovery as before
        if (_distributedLockService == null)
        {
            return await _redisLuaScriptService.RecoverStuckJobs(_database, timeoutUnixTime, maxRetries);
        }

        // Use distributed locking to ensure only one instance performs recovery
        var lockKey = "ae:job_recovery:lock";
        var lockValue = $"worker_{Guid.NewGuid()}"; // Unique identifier for this recovery attempt
        var lockTimeout = TimeSpan.FromMinutes(5); // Reasonable timeout for recovery operation
        
        var lockAcquired = await _distributedLockService.TryAcquireLockAsync(
            lockKey, 
            lockValue, 
            lockTimeout, 
            cancellationToken);

        if (!lockAcquired)
        {
            _logger.LogDebug("Another instance holds the recovery lock, skipping this cycle");
            return 0; // No recovery performed
        }

        try
        {
            _logger.LogDebug("Recovery lock acquired, starting job recovery process");
            return await _redisLuaScriptService.RecoverStuckJobs(_database, timeoutUnixTime, maxRetries);
        }
        finally
        {
            // Always attempt to release the lock
            await _distributedLockService.TryReleaseLockAsync(lockKey, lockValue, CancellationToken.None);
        }
    }

    // Other interface methods remain unchanged...
}
```

### Service Registration

The lock service will be registered when using Redis job store:

```csharp
public static IAsyncEndpointsBuilder AddRedisJobStore(this IAsyncEndpointsBuilder builder, string connectionString, AsyncEndpointsRecoveryConfigurations? recoveryConfig = null)
{
    var services = builder.Services;

    // Register Redis connection
    services.AddSingleton<IConnectionMultiplexer>(provider => ConnectionMultiplexer.Connect(connectionString));
    
    // Register job store dependencies
    services.AddSingleton<IJobHashConverter, JobHashConverter>();
    services.AddSingleton<IRedisLuaScriptService, RedisLuaScriptService>();
    
    // Register distributed lock service
    services.AddSingleton<IDistributedLockService>(provider =>
    {
        var connectionMultiplexer = provider.GetRequiredService<IConnectionMultiplexer>();
        var database = connectionMultiplexer.GetDatabase();
        var logger = provider.GetRequiredService<ILogger<RedisDistributedLockService>>();
        return new RedisDistributedLockService(database, logger);
    });
    
    // Register Redis job store with the lock service
    services.AddSingleton<IJobStore>(provider =>
    {
        var connectionMultiplexer = provider.GetRequiredService<IConnectionMultiplexer>();
        var database = connectionMultiplexer.GetDatabase();
        var logger = provider.GetRequiredService<ILogger<RedisJobStore>>();
        var dateTimeProvider = provider.GetRequiredService<IDateTimeProvider>();
        var jobHashConverter = provider.GetRequiredService<IJobHashConverter>();
        var serializer = provider.GetRequiredService<ISerializer>();
        var redisLuaScriptService = provider.GetRequiredService<IRedisLuaScriptService>();
        var metrics = provider.GetRequiredService<IAsyncEndpointsObservability>();
        var distributedLockService = provider.GetRequiredService<IDistributedLockService>();

        return new RedisJobStore(
            logger, 
            database, 
            dateTimeProvider, 
            jobHashConverter, 
            serializer, 
            redisLuaScriptService, 
            metrics,
            distributedLockService // Pass the lock service
        );
    });

    // ... rest of configuration ...
}
```

### Alternative: Conditional Service Registration

If we want to ensure the lock service is only used when recovery is enabled:

```csharp
// Only register distributed lock service if recovery is enabled
if (recoveryConfig?.EnableDistributedJobRecovery == true)
{
    services.AddSingleton<IDistributedLockService>(provider =>
    {
        var connectionMultiplexer = provider.GetRequiredService<IConnectionMultiplexer>();
        var database = connectionMultiplexer.GetDatabase();
        var logger = provider.GetRequiredService<ILogger<RedisDistributedLockService>>();
        return new RedisDistributedLockService(database, logger);
    });
}
```

## Benefits of This Approach

### Internal Implementation
- Locking concerns are kept internal to the Redis job store implementation
- The `IJobStore` interface remains clean and simple
- No changes required to other job store implementations

### Modularity
- The distributed lock service is separate and focused on one responsibility
- Can be tested independently from the job store
- Reusable for other Redis-based coordination needs in the future

### Backward Compatibility
- The lock service parameter is optional to maintain backward compatibility
- Existing implementations continue to work without changes
- Recovery behavior is enhanced only when the lock service is available

### Testability
- The lock service can be easily mocked in unit tests
- Job store recovery logic can be tested separately from locking
- The Redis job store can be tested with or without the lock service

## Implementation Considerations

### Performance
- Lock acquisition is a single fast Redis operation
- If lock acquisition fails, the operation terminates immediately with minimal resource usage
- Only one instance performs the actual recovery work

### Error Handling
- All locking operations are wrapped in try-catch with appropriate logging
- If lock acquisition fails, recovery is skipped gracefully
- Lock release failures are logged but don't throw exceptions
- The system gracefully handles cases where the lock service is not available

### Configuration
- The lock service is automatically registered when using Redis job store with recovery enabled
- No additional configuration required for basic usage
- Lock timeouts can be configured if needed

## Migration Path

1. Create the `IDistributedLockService` interface and `RedisDistributedLockService` implementation
2. Update the `RedisJobStore` constructor to accept the lock service as an optional parameter
3. Modify the `RecoverStuckJobs` method to use the lock service internally
4. Update service registration to include the lock service
5. Test with single and multiple instance deployments
6. Deploy gradually to production environments

## Alternative Approaches

### Simple Conditional Registration
Only inject the lock service when recovery is enabled, which could be implemented via a factory pattern or conditional service registration.

### Lock Timeout Configuration
Consider making the lock timeout configurable based on the expected recovery time and recovery check interval.

## Conclusion

This approach effectively solves the race condition problem for the Redis job store while keeping the locking mechanism internal to the implementation. The `IJobStore` interface remains unchanged, preserving backward compatibility, while the Redis implementation gains the ability to coordinate recovery operations across multiple instances through the distributed lock service.

The solution is modular, testable, and follows good separation of concerns principles. The lock service can be enhanced in the future to support different backends if needed, while the core job store interface remains stable.