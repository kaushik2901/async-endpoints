# Distributed Recovery Strategy for AsyncEndpoints

## Overview

When deploying AsyncEndpoints in a distributed environment with multiple worker instances, special considerations must be made for the job recovery service. This document outlines the challenges that arise and the recommended solutions for implementing distributed recovery.

## The Challenge

In a multi-instance deployment where multiple workers are running the `DistributedJobRecoveryService`, there's a potential for:

1. **Duplicate Recovery Work** - Multiple instances scanning and attempting to recover the same stuck jobs
2. **Resource Waste** - Multiple workers performing identical recovery operations
3. **Performance Overhead** - Multiple simultaneous scans on the storage backend (Redis)
4. **Log Noise** - Multiple instances reporting the same recovery operations

## Current Behavior Analysis

The current `DistributedJobRecoveryService` operates as follows:

- Runs at configured intervals (default: every 5 minutes)
- Scans all jobs in the storage system
- Identifies jobs that have been "in progress" longer than the configured timeout
- Recovers stuck jobs using atomic operations (Lua scripts for Redis)

While the underlying storage operations are atomic (preventing data corruption), multiple instances will still perform the same scan and recovery work.

## Recommended Solutions

### 1. Distributed Locking (Recommended Approach)

Implement a distributed lock to ensure only one instance performs recovery at a time:

```csharp
private async Task RecoverStuckJobs(CancellationToken cancellationToken)
{
    var lockKey = "ae:recovery:lock";
    var lockValue = $"{Environment.MachineName}_{Environment.ProcessId}_{Guid.NewGuid()}";
    var lockTimeout = TimeSpan.FromSeconds(_recoveryInterval.TotalSeconds);
    
    // Attempt to acquire distributed lock with NX (Not Exists) option
    var lockAcquired = await database.StringSetAsync(
        lockKey, 
        lockValue, 
        lockTimeout,
        When.NotExists,
        CommandFlags.HighPriority);
    
    if (!lockAcquired)
    {
        _logger.LogDebug("Another instance is already performing job recovery, skipping this cycle");
        return;
    }
    
    try
    {
        // Perform recovery work
        var timeoutUnixTime = _dateTimeProvider.DateTimeOffsetNow.AddMinutes(-_jobTimeoutMinutes).ToUnixTimeSeconds();
        var recoveredCount = await _jobStore.RecoverStuckJobs(
            timeoutUnixTime,
            _maxRetries,
            _retryDelayBaseSeconds,
            cancellationToken);

        if (recoveredCount > 0)
        {
            _logger.LogInformation("Recovered {RecoveredCount} stuck jobs", recoveredCount);
        }
    }
    finally
    {
        // Only release the lock if we still own it (prevent releasing another instance's lock)
        var currentValue = await database.StringGetAsync(lockKey);
        if (currentValue == lockValue)
        {
            await database.KeyDeleteAsync(lockKey, CommandFlags.HighPriority);
        }
    }
}
```

**Benefits:**
- Ensures only one instance performs recovery at a time
- Prevents duplicate work and resource waste
- Simple and reliable implementation
- Fail-safe (lock expires automatically if instance crashes)

### 2. Leader Election Pattern

Use Redis keys for leader election where one instance is elected as the recovery leader:

```csharp
// Acquire leadership with lease renewal
var leaderKey = "ae:recovery:leader";
var instanceId = $"{Environment.MachineName}_{Environment.ProcessId}";
var leaseDuration = TimeSpan.FromMinutes(10);

var becameLeader = await database.StringSetAsync(
    leaderKey, 
    instanceId, 
    leaseDuration, 
    When.NotExists);
```

### 3. Work Partitioning (Advanced)

Divide recovery work among instances by partitioning the job space:

```csharp
// Each instance only processes jobs in a specific hash range
var instanceHash = Math.Abs(Environment.MachineName.GetHashCode()) % totalInstances;
var jobsToProcess = allJobs.Where(j => Math.Abs(j.Id.GetHashCode()) % totalInstances == instanceHash);
```

**Note:** This approach is more complex and may result in uneven work distribution.

## Implementation Considerations

### For Redis Deployments

- Use Redis distributed locks (SET NX EX commands)
- Consider lock timeout slightly longer than the expected recovery operation time
- Always use unique lock values to prevent releasing another instance's lock

### For In-Memory Deployments

- In-memory stores don't support job recovery (SupportsJobRecovery = false)
- This issue doesn't apply to single-instance in-memory deployments

### For Other Storage Backends

- Implement similar locking mechanisms appropriate for the storage system
- Ensure atomic operations when updating job states
- Consider the storage system's consistency model

## Configuration Recommendations

### Production Multi-Instance Deployments

```csharp
// Enable recovery service (default: true)
services.AddAsyncEndpointsWorker(recoveryConfig =>
{
    recoveryConfig.EnableDistributedJobRecovery = true;
    recoveryConfig.RecoveryCheckIntervalSeconds = 300; // 5 minutes
    recoveryConfig.JobTimeoutMinutes = 10; // Adjust based on job expectations
    // Note: Distributed locking handles multiple instance coordination
});
```

### Single Instance Deployments

- No special coordination needed
- Default configuration is sufficient

## Best Practices

1. **Always Enable Distributed Locking** for multi-instance deployments
2. **Monitor Recovery Metrics** to ensure recovery is happening appropriately
3. **Set Appropriate Timeouts** - job timeout should be longer than expected processing time
4. **Test Recovery Behavior** in your deployment environment
5. **Consider Logging Differentiation** to identify which instance performed recovery

## Migration Path

For existing multi-instance deployments, implement distributed locking by:

1. Add distributed lock logic to the `DistributedJobRecoveryService.RecoverStuckJobs` method
2. Ensure all instances use the same lock key strategy
3. Test with multiple instances to validate only one performs recovery per cycle
4. Monitor logs to confirm the solution works as expected

## Conclusion

The distributed recovery challenge in multi-instance AsyncEndpoints deployments can be effectively addressed through distributed locking mechanisms. This ensures optimal resource utilization while maintaining the reliability and safety of the job recovery process across all instances.

The recommended approach is to implement distributed locking using Redis atomic operations, which provides the best balance of simplicity, reliability, and performance.