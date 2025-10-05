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

## Most Recommended Solution: Job Lease with Distributed Recovery

The most robust and scalable approach is to implement a **Job Lease with Distributed Recovery** mechanism that works well in a multi-worker distributed environment.

### Core Components

1. **Job Lease Timeouts**: Each job gets a lease that expires after a configurable time
2. **Distributed Recovery Service**: Multiple workers can participate in recovery without conflicts
3. **Atomic Operations**: All recovery operations use Redis Lua scripts for atomicity

### Implementation Details

#### 1. Enhanced Job Model
```csharp
// Add to Job class
public DateTimeOffset? LeaseExpiration { get; set; } = null;
public Guid? LeasingWorkerId { get; set; } = null;
```

#### 2. Lease-Based Job Claiming
Update the existing Lua script in `ClaimNextJobForWorker` to include lease expiration:

```csharp
// In RedisJobStore.cs - update the Lua script to include lease time
var luaScript = @"
    local jobKey = ARGV[1]
    local expectedStatus1 = ARGV[2]  -- Queued
    local expectedStatus2 = ARGV[3]  -- Scheduled  
    local newStatus = ARGV[4]        -- InProgress
    local newWorkerId = ARGV[5]
    local newStartedAt = ARGV[6]
    local newLastUpdatedAt = ARGV[7]
    local queueKey = ARGV[8]
    local jobId = ARGV[9]
    local currentTime = ARGV[10]
    local leaseDuration = ARGV[11]   -- Lease duration in seconds

    -- Get required fields atomically
    local currentStatus = redis.call('HGET', jobKey, 'Status')
    local currentWorkerId = redis.call('HGET', jobKey, 'WorkerId')

    -- Check if job can be claimed - all checks in one atomic operation
    if currentWorkerId and currentWorkerId ~= '' then
        return redis.error_reply('ALREADY_ASSIGNED')
    end

    if not (currentStatus == expectedStatus1 or currentStatus == expectedStatus2) then
        return redis.error_reply('WRONG_STATUS')
    end

    -- Get all fields we need to return the complete job object
    local currentId = redis.call('HGET', jobKey, 'Id')
    local currentName = redis.call('HGET', jobKey, 'Name')
    local currentHeaders = redis.call('HGET', jobKey, 'Headers')
    local currentRouteParams = redis.call('HGET', jobKey, 'RouteParams')
    local currentQueryParams = redis.call('HGET', jobKey, 'QueryParams')
    local currentPayload = redis.call('HGET', jobKey, 'Payload')
    local currentResult = redis.call('HGET', jobKey, 'Result')
    local currentError = redis.call('HGET', jobKey, 'Error')
    local currentRetryCount = redis.call('HGET', jobKey, 'RetryCount')
    local currentMaxRetries = redis.call('HGET', jobKey, 'MaxRetries')
    local currentCreatedAt = redis.call('HGET', jobKey, 'CreatedAt')
    local currentCompletedAt = redis.call('HGET', jobKey, 'CompletedAt')
    local currentLastUpdatedAt = redis.call('HGET', jobKey, 'LastUpdatedAt')

    -- Calculate lease expiration (current time + lease duration)
    local leaseExpiration = tonumber(currentTime) + tonumber(leaseDuration)

    -- Claim the job atomically with lease information
    redis.call('HSET', jobKey, 
        'Status', newStatus,
        'WorkerId', newWorkerId,
        'StartedAt', newStartedAt,
        'LastUpdatedAt', newLastUpdatedAt,
        'LeaseExpiration', leaseExpiration,
        'LeasingWorkerId', newWorkerId)
    redis.call('ZREM', queueKey, jobId)

    -- Return all fields needed to construct the complete job object
    return { 
        currentId, currentName, newStatus, currentHeaders, currentRouteParams, 
        currentQueryParams, currentPayload, currentResult, currentError, 
        currentRetryCount, currentMaxRetries, currentWorkerId, 
        currentCreatedAt, newStartedAt, currentCompletedAt, newLastUpdatedAt,
        leaseExpiration
    }
";
```

#### 3. Distributed Recovery Service
Create a background service that safely recovers stuck jobs across multiple workers:

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
        // Use a Lua script to atomically find and recover stuck jobs
        var luaScript = @"
            local timeoutUnixTime = tonumber(ARGV[1])
            local maxRetries = tonumber(ARGV[2])
            local retryDelayBaseSeconds = tonumber(ARGV[3])
            local recoveryWorkerId = ARGV[4]
            local currentTime = ARGV[5]
            
            -- Find all expired leases (jobs where LeaseExpiration < current time)
            -- This requires maintaining a separate sorted set for lease tracking
            -- or querying all in-progress jobs and checking their lease expiration
            
            -- For Redis hash + sorted set approach, we'd maintain a separate index
            -- Let's use a different approach: scan all job hashes
            
            local cursor = 0
            local recoveredCount = 0
            
            repeat
                local result = redis.call('SCAN', cursor, 'MATCH', 'ae:job:*', 'COUNT', 100)
                cursor = tonumber(result[1])
                local keys = result[2]
                
                for _, jobKey in ipairs(keys) do
                    local leaseExpiration = redis.call('HGET', jobKey, 'LeaseExpiration')
                    local status = redis.call('HGET', jobKey, 'Status')
                    local retryCount = redis.call('HGET', jobKey, 'RetryCount') or 0
                    local maxRetriesForJob = redis.call('HGET', jobKey, 'MaxRetries') or maxRetries
                    
                    if status == '300' and leaseExpiration and tonumber(leaseExpiration) < timeoutUnixTime then
                        -- This job is expired and in progress, try to recover it
                        -- Use atomic operation to ensure only one worker recovers it
                        
                        local currentWorkerId = redis.call('HGET', jobKey, 'WorkerId')
                        
                        -- Attempt to claim this expired job
                        if leaseExpiration then
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
                                    'WorkerId', '',
                                    'LeasingWorkerId', '',
                                    'LeaseExpiration', '',
                                    'LastUpdatedAt', currentTime)
                                
                                -- Add back to the queue
                                local jobId = string.gsub(jobKey, 'ae:job:', '')
                                redis.call('ZADD', 'ae:jobs:queue', retryUntil, jobId)
                                
                                recoveredCount = recoveredCount + 1
                            else
                                -- Mark as failed permanently
                                redis.call('HSET', jobKey,
                                    'Status', '500', -- Failed
                                    'Error', 'Job failed after maximum retries',
                                    'WorkerId', '',
                                    'LeasingWorkerId', '',
                                    'LeaseExpiration', '',
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
                Guid.NewGuid().ToString(), // worker ID for this recovery attempt
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

#### 4. Startup Recovery
Implement startup recovery to handle jobs that were stuck at the time of shutdown:

```csharp
public class StartupRecoveryService
{
    private readonly ILogger<StartupRecoveryService> _logger;
    private readonly IJobStore _jobStore;
    private readonly IDateTimeProvider _dateTimeProvider;
    
    public async Task RecoverJobsOnStartup(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting startup job recovery");
        
        // Find jobs that were in progress at the time of shutdown
        // This can be done with a scan for jobs with InProgress status
        // Implementation similar to the distributed recovery service
        // but run once at startup
        
        await RecoverStuckJobs(cancellationToken);
    }
}
```

## Why This Approach is Best for Distributed Environments

1. **Atomic Operations**: Uses Lua scripts to ensure that recovery operations are atomic and avoid race conditions between multiple workers
2. **No Conflicts**: Multiple workers can safely run the recovery service without conflicting with each other
3. **Scalable**: The recovery service can run on all or some workers without coordination
4. **Efficient**: Uses Redis SCAN operations to efficiently find stuck jobs without blocking
5. **Safe**: Only recovers jobs that have actually timed out, avoiding interference with currently processing jobs
6. **Configurable**: Timeout values can be adjusted based on job requirements

## Implementation Steps

1. **Phase 1**: Add lease expiration to job model and update claiming logic
2. **Phase 2**: Implement distributed recovery background service
3. **Phase 3**: Add startup recovery mechanism
4. **Phase 4**: Test with multiple workers and various failure scenarios

## Configuration

Add these configuration options:
- `JobLeaseTimeoutMinutes`: How long a worker has to complete a job (default: 30 minutes)
- `RecoveryCheckIntervalSeconds`: How often recovery service runs (default: 300 seconds)
- `EnableDistributedRecovery`: Whether to enable the recovery service (default: true)

## Advantages of This Solution

- ✅ **Robust**: Handles various failure scenarios including crashes and network partitions
- ✅ **Scalable**: Works efficiently with multiple workers without coordination overhead
- ✅ **Safe**: Atomic operations prevent race conditions and double-processing
- ✅ **Configurable**: Timeout values can be tailored to specific job requirements
- ✅ **Self-healing**: System automatically recovers from stuck jobs
- ✅ **Distributed**: Multiple workers can participate in recovery without conflicts
- ✅ **Low overhead**: Efficient Redis operations with minimal impact on performance

This solution provides the most robust and scalable approach for handling job recovery in a distributed environment with multiple workers.