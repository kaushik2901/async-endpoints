# Redis Job Store Recovery Solution - Recommended Approach

## Problem Statement

The current Redis job store implementation in AsyncEndpoints does not re-pick jobs that were added after a system restart. Specifically, jobs that were in the "in progress" state when the system crashed or was shut down remain stuck and are never processed again.

## Root Cause Analysis

### Current Implementation
- Jobs are stored as Redis hashes with keys like `ae:job:{jobId}`
- Queued jobs are maintained in a sorted set `ae:jobs:queue` with timestamps as scores
- When a job is claimed for processing, it's atomically removed from the queue and its status is updated to `InProgress`
- Only jobs with status `Queued` or `Scheduled` are considered for claiming by workers
- No recovery mechanism exists for jobs that become stuck in `InProgress` state

### The Issue
When the system restarts:
1. Background services start fresh
2. Jobs that were in progress when the system stopped remain in `InProgress` status
3. These jobs are not reconsidered for processing since they're not in the queue
4. The system only processes jobs with status `Queued` or `Scheduled`

## Most Recommended Solution: Job Lease with Distributed Recovery (Using StartedAt)

The most robust and scalable approach is to implement a **Job Lease with Distributed Recovery** mechanism that works well in a multi-worker distributed environment, using the existing `StartedAt` field for timeout calculations.

### Core Components

1. **Time-based Job Recovery**: Use `StartedAt` field with a configured timeout to detect stuck jobs
2. **Distributed Recovery Service**: Multiple workers can participate in recovery without conflicts
3. **Atomic Operations**: All recovery operations use Redis Lua scripts for atomicity

### Implementation Details

#### 1. Lease-Based Job Claiming (Updated)
The existing Lua script in `ClaimNextJobForWorker` already sets the `StartedAt` field, which we'll use for timeout calculations:

```csharp
// In RedisJobStore.cs - the existing Lua script already handles StartedAt
// No changes needed to the claiming logic, we just use StartedAt for recovery
```

#### 2. Distributed Recovery Service
Create a background service that safely recovers stuck jobs across multiple workers using `StartedAt`:

```csharp
public class DistributedJobRecoveryService : BackgroundService
{
    private readonly ILogger<DistributedJobRecoveryService> _logger;
    private readonly IJobStore _jobStore;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly TimeSpan _recoveryInterval;
    private readonly int _jobTimeoutMinutes;

    public DistributedJobRecoveryService(
        ILogger<DistributedJobRecoveryService> logger,
        IJobStore jobStore,
        IDateTimeProvider dateTimeProvider,
        IOptions<AsyncEndpointsConfigurations> configurations)
    {
        _logger = logger;
        _jobStore = jobStore;
        _dateTimeProvider = dateTimeProvider;
        _recoveryInterval = TimeSpan.FromMinutes(5); // Configurable
        _jobTimeoutMinutes = configurations.Value.JobManagerConfiguration.JobTimeoutMinutes;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecoverStuckJobs(stoppingToken);
                await Task.Delay(_recoveryInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during job recovery");
                // Continue despite errors to keep the recovery process running
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task RecoverStuckJobs(CancellationToken cancellationToken)
    {
        // Use a Lua script to atomically find and recover stuck jobs using StartedAt
        var luaScript = @"
            local timeoutUnixTime = tonumber(ARGV[1])
            local maxRetries = tonumber(ARGV[2])
            local retryDelayBaseSeconds = tonumber(ARGV[3])
            local currentTime = ARGV[4]
            
            local recoveredCount = 0
            local cursor = 0
            
            -- Use SCAN to efficiently find all job keys
            repeat
                local result = redis.call('SCAN', cursor, 'MATCH', 'ae:job:*', 'COUNT', 100)
                cursor = tonumber(result[1])
                local keys = result[2]
                
                for _, jobKey in ipairs(keys) do
                    local status = redis.call('HGET', jobKey, 'Status')
                    local startedAt = redis.call('HGET', jobKey, 'StartedAt')
                    local retryCount = redis.call('HGET', jobKey, 'RetryCount') or 0
                    local maxRetriesForJob = redis.call('HGET', jobKey, 'MaxRetries') or maxRetries
                    
                    -- Check if job is InProgress (status 300) and started more than timeout ago
                    if status == '300' and startedAt then
                        -- Parse ISO 8601 datetime string to compare with timeout
                        -- For efficiency, store StartedAt as Unix timestamp too (optional enhancement)
                        local jobStartDateTime = startedAt
                        
                        -- Using a simplified approach: parse the StartedAt and check if it's too old
                        -- In practice, we'd store a numeric timestamp for easier comparison
                        -- For this script, we'll assume startedAt is in ISO format
                        -- and we'll use the timeoutUnixTime to compare
                        
                        -- If we store StartedAt as Unix timestamp, comparison becomes:
                        -- if tonumber(startedAt) < timeoutUnixTime then
                        -- For ISO format, we'd need Redis to parse it, which is inefficient
                        -- Better approach: store both StartedAt (for API) and StartedAtUnix (for recovery)
                        
                        -- Option 1: Store an additional StartedAtUnix field when claiming
                        local startedAtUnix = redis.call('HGET', jobKey, 'StartedAtUnix')
                        
                        if startedAtUnix and tonumber(startedAtUnix) < timeoutUnixTime then
                            retryCount = tonumber(retryCount)
                            maxRetriesForJob = tonumber(maxRetriesForJob)
                            
                            if retryCount < maxRetriesForJob then
                                -- Increment retry count and reschedule
                                local newRetryCount = retryCount + 1
                                local newRetryDelay = math.pow(2, newRetryCount) * retryDelayBaseSeconds
                                local retryUntil = tonumber(currentTime) + newRetryDelay
                                
                                -- Update the job to scheduled status
                                redis.call('HSET', jobKey, 
                                    'Status', '200', -- Scheduled
                                    'RetryCount', newRetryCount,
                                    'RetryDelayUntil', retryUntil,
                                    'WorkerId', '', -- Release worker assignment
                                    'StartedAt', '', -- Clear started time
                                    'StartedAtUnix', '', -- Clear started time
                                    'LastUpdatedAt', currentTime)
                                
                                -- Get job ID from key and add back to the queue
                                local jobId = string.gsub(jobKey, 'ae:job:', '')
                                redis.call('ZADD', 'ae:jobs:queue', retryUntil, jobId)
                                
                                recoveredCount = recoveredCount + 1
                            else
                                -- Mark as failed permanently
                                redis.call('HSET', jobKey,
                                    'Status', '500', -- Failed
                                    'Error', 'Job failed after maximum retries',
                                    'WorkerId', '',
                                    'StartedAt', '',
                                    'StartedAtUnix', '',
                                    'LastUpdatedAt', currentTime)
                            end
                        end
                    end
                end
            until cursor == 0
            
            return recoveredCount
        "

        var timeoutUnixTime = _dateTimeProvider.UtcNow.AddMinutes(-_jobTimeoutMinutes).ToUnixTimeSeconds();
        var result = await _database.ScriptEvaluateAsync(luaScript, 
            values: [
                timeoutUnixTime.ToString(),
                AsyncEndpointsConstants.MaximumRetries.ToString(),
                "5", // retry delay base, should come from config
                _dateTimeProvider.UtcNow.ToUnixTimeSeconds().ToString()
            ]);

        var recoveredCount = (int)(long)result;
        if (recoveredCount > 0)
        {
            _logger.LogInformation("Recovered {RecoveredCount} stuck jobs", recoveredCount);
        }
    }
}
```

#### 3. Enhanced Job Claiming to Support Recovery
To make the recovery efficient, we should add a Unix timestamp version of `StartedAt`:

```csharp
// In the existing ClaimNextJobForWorker Lua script, add:
-- Calculate and store StartedAt as Unix timestamp for efficient recovery
local startedAtDateTime = ARGV[6]  -- This is the ISO datetime string
-- For recovery efficiency, also store as Unix timestamp
-- We can add this during claiming:
local startedUnix = tonumber(currentTime)  -- Use the current time provided

-- In the job claiming script, after setting StartedAt:
redis.call('HSET', jobKey, 
    'Status', newStatus,
    'WorkerId', newWorkerId,
    'StartedAt', newStartedAt,
    'StartedAtUnix', startedUnix,  -- Add Unix timestamp
    'LastUpdatedAt', newLastUpdatedAt)
```

#### 4. Startup Recovery
Implement startup recovery to handle jobs that were stuck at the time of shutdown:

```csharp
public class StartupRecoveryService
{
    private readonly ILogger<StartupRecoveryService> _logger;
    private readonly IJobStore _jobStore;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly int _jobTimeoutMinutes;
    
    public async Task RecoverJobsOnStartup(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting startup job recovery");
        
        // Run immediate recovery for any jobs that got stuck during shutdown
        await RecoverStuckJobs(cancellationToken);
    }
    
    private async Task RecoverStuckJobs(CancellationToken cancellationToken)
    {
        // Similar implementation to the distributed recovery service
    }
}
```

## Why This Approach is Best for Distributed Environments

1. **Uses Existing Fields**: Leverages the existing `StartedAt` field, requiring minimal changes
2. **Atomic Operations**: Uses Lua scripts to ensure that recovery operations are atomic and avoid race conditions between multiple workers
3. **No Conflicts**: Multiple workers can safely run the recovery service without conflicting with each other
4. **Scalable**: The recovery service can run on all or some workers without coordination overhead
5. **Efficient**: Uses Redis SCAN operations to efficiently find stuck jobs without blocking
6. **Safe**: Only recovers jobs that have actually timed out, avoiding interference with currently processing jobs
7. **Minimal Overhead**: Only adds one additional field (`StartedAtUnix`) for efficient recovery

## Implementation Steps

1. **Phase 1**: Update job claiming logic to add `StartedAtUnix` timestamp
2. **Phase 2**: Implement distributed recovery background service
3. **Phase 3**: Add startup recovery mechanism
4. **Phase 4**: Test with multiple workers and various failure scenarios

## Configuration

Add these configuration options:
- `JobTimeoutMinutes`: How long a worker has to complete a job (default: 30 minutes)
- `RecoveryCheckIntervalSeconds`: How often recovery service runs (default: 300 seconds)
- `EnableDistributedRecovery`: Whether to enable the recovery service (default: true)

## Advantages of This Solution

- ✅ **Robust**: Handles various failure scenarios including crashes and network partitions
- ✅ **Scalable**: Works efficiently with multiple workers without coordination overhead
- ✅ **Safe**: Atomic operations prevent race conditions and double-processing
- ✅ **Configurable**: Timeout values can be tailored to specific job requirements
- ✅ **Self-healing**: System automatically recovers from stuck jobs
- ✅ **Distributed**: Multiple workers can participate in recovery without conflicts
- ✅ **Minimal Changes**: Uses existing `StartedAt` field with only one additional timestamp field
- ✅ **Low overhead**: Efficient Redis operations with minimal impact on performance

This solution provides the most robust and scalable approach for handling job recovery in a distributed environment with multiple workers, using the existing `StartedAt` field as the foundation for timeout detection.