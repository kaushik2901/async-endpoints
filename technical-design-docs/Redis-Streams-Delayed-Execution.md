# Implementing Delayed Execution in Redis Streams for AsyncEndpoints

## Overview

This document outlines the approaches for implementing delayed execution (retry delays and scheduled jobs) in a Redis Streams-based implementation for AsyncEndpoints, since Redis Streams don't natively support delayed execution like Sorted Sets do.

## Current Delayed Execution Implementation

In the current Sorted Sets implementation, delayed execution works by:
1. Using timestamps as scores in sorted sets
2. Only processing jobs where `currentTime >= job.RetryDelayUntil`
3. Storing jobs with future timestamps until they become eligible for processing

This approach doesn't directly translate to Redis Streams, which are designed for immediate processing.

## Approaches for Delayed Execution with Redis Streams

### Approach 1: Dual Queue System (Recommended)

Implement a hybrid approach using both Redis Streams and Sorted Sets:

```csharp
public class DualQueueRedisJobStore : IJobStore
{
    private readonly string _activeStreamKey = "ae:jobs:stream";
    private readonly string _delayedQueueKey = "ae:jobs:delayed";
    private readonly string _inProgressStreamKey = "ae:jobs:inprogress:stream";
    
    public async Task<MethodResult> CreateJob(Job job, CancellationToken cancellationToken)
    {
        var jobKey = GetJobKey(job.Id);
        var hashEntries = _jobHashConverter.ConvertToHashEntries(job);
        await _database.HashSetAsync(jobKey, hashEntries);

        if (job.RetryDelayUntil.HasValue && job.RetryDelayUntil > _dateTimeProvider.UtcNow)
        {
            // Job is delayed - add to delayed queue sorted set
            var score = job.RetryDelayUntil.Value.ToUnixTimeSeconds();
            await _database.SortedSetAddAsync(_delayedQueueKey, job.Id.ToString(), score);
            
            // Also add to stream for tracking, with delayed status indicator
            var streamEntry = new NameValueEntry[]
            {
                new NameValueEntry("jobId", job.Id.ToString()),
                new NameValueEntry("status", "delayed"),
                new NameValueEntry("delayUntil", job.RetryDelayUntil.Value.ToUnixTimeSeconds().ToString()),
                new NameValueEntry("addedAt", _dateTimeProvider.DateTimeOffsetNow.ToUnixTimeSeconds().ToString())
            };
            await _database.StreamAddAsync(_activeStreamKey, streamEntry);
        }
        else
        {
            // Job is ready to process - add to active stream
            var streamEntry = new NameValueEntry[]
            {
                new NameValueEntry("jobId", job.Id.ToString()),
                new NameValueEntry("status", ((int)job.Status).ToString()),
                new NameValueEntry("timestamp", _dateTimeProvider.DateTimeOffsetNow.ToUnixTimeSeconds().ToString())
            };
            await _database.StreamAddAsync(_activeStreamKey, streamEntry);
        }
        
        return MethodResult.Success();
    }

    public async Task<MethodResult<Job>> ClaimNextJobForWorker(Guid workerId, CancellationToken cancellationToken)
    {
        // First, move any eligible delayed jobs to the active queue
        await ProcessDelayedJobs(cancellationToken);

        // Now try to claim a job from the active stream
        try
        {
            // Create consumer group if it doesn't exist
            try
            {
                await _database.StreamCreateConsumerGroupAsync(_activeStreamKey, "workers", StreamPosition.Beginning);
            }
            catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
            {
                // Group already exists, continue
            }

            // Read from the stream to get the next available job
            var entries = await _database.StreamReadGroupAsync(
                _activeStreamKey, 
                "workers", 
                workerId.ToString(), // Consumer name
                StreamPosition.NewEntries, // Read new entries
                count: 1 // Only take one job
            );

            if (entries.Length == 0)
            {
                return MethodResult<Job>.Success(default);
            }

            var streamEntry = entries[0].Values;
            var jobIdString = streamEntry.First(x => x.Name == "jobId").Value;
            
            if (!Guid.TryParse(jobIdString, out var jobId))
            {
                return MethodResult<Job>.Success(default);
            }

            // Get the full job from hash storage
            var result = await GetJobById(jobId, cancellationToken);
            if (!result.IsSuccess || result.Data == null)
            {
                return MethodResult<Job>.Success(default);
            }

            var job = result.Data;
            
            // Only claim jobs that are ready to process (not delayed)
            if (job.Status == JobStatus.Scheduled && job.RetryDelayUntil.HasValue && job.RetryDelayUntil > _dateTimeProvider.UtcNow)
            {
                // Put back into delayed queue and return null
                var score = job.RetryDelayUntil.Value.ToUnixTimeSeconds();
                await _database.SortedSetAddAsync(_delayedQueueKey, job.Id.ToString(), score);
                
                // Acknowledge the stream entry to remove it
                await _database.StreamAcknowledgeAsync(_activeStreamKey, "workers", entries[0].Id);
                return MethodResult<Job>.Success(default);
            }

            // Update job status and assign to worker using atomic Lua script
            var luaScript = @"
                local jobKey = ARGV[1]
                local currentStatus = redis.call('HGET', jobKey, 'Status')
                local currentWorkerId = redis.call('HGET', jobKey, 'WorkerId')
                
                -- Check if job is already assigned or not in the correct state
                if currentWorkerId and currentWorkerId ~= '' then
                    return redis.error_reply('ALREADY_ASSIGNED')
                end
                
                if currentStatus ~= ARGV[2] and currentStatus ~= ARGV[3] and currentStatus ~= ARGV[4] then
                    return redis.error_reply('WRONG_STATUS')
                end
                
                -- Update job status and assign to worker
                local nowIso = ARGV[6]
                redis.call('HSET', jobKey, 
                    'Status', ARGV[5],
                    'WorkerId', ARGV[7],
                    'StartedAt', nowIso,
                    'LastUpdatedAt', nowIso)
                
                -- Acknowledge the stream entry so it won't be processed by other workers
                redis.call('XACK', ARGV[8], ARGV[9], ARGV[10])
                
                -- Return the updated job hash
                return redis.call('HGETALL', jobKey)
            ";

            var now = _dateTimeProvider.DateTimeOffsetNow;
            var scriptResult = await _database.ScriptEvaluateAsync(luaScript, 
                keys: new RedisKey[] { GetJobKey(jobId) },
                values: new RedisValue[] {
                    GetJobKey(jobId),                        // ARGV[1] - job key
                    ((int)JobStatus.Queued).ToString(),      // ARGV[2] - queued status
                    ((int)JobStatus.Scheduled).ToString(),   // ARGV[3] - scheduled status
                    ((int)JobStatus.InProgress).ToString(),  // ARGV[4] - in progress status (for recovery)
                    ((int)JobStatus.InProgress).ToString(),  // ARGV[5] - new status
                    now.ToString("O"),                       // ARGV[6] - current time
                    workerId.ToString(),                     // ARGV[7] - worker ID
                    _activeStreamKey,                        // ARGV[8] - stream key
                    "workers",                               // ARGV[9] - consumer group
                    entries[0].Id                            // ARGV[10] - stream entry ID
                }
            );

            if (scriptResult.Resp3Type == ResultType.Error)
            {
                var error = scriptResult.ToString();
                if (error.Contains("ALREADY_ASSIGNED") || error.Contains("WRONG_STATUS"))
                {
                    return MethodResult<Job>.Success(default);
                }
                return MethodResult<Job>.Failure(AsyncEndpointError.FromCode("JOB_CLAIM_ERROR", error));
            }

            // Reconstruct job from returned hash and return
            var hashResult = (RedisValue[])scriptResult;
            var reconstructedJob = _jobHashConverter.ConvertFromHashEntries(
                hashResult
                    .Where((value, index) => index % 2 == 0) // Keys at even indices
                    .Zip(hashResult.Where((value, index) => index % 2 == 1), (key, val) => new { Key = (string)key, Value = (string)val })
                    .ToDictionary(x => x.Key, x => x.Value)
            );
            
            return MethodResult<Job>.Success(reconstructedJob);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error claiming job for worker {WorkerId}", workerId);
            return MethodResult<Job>.Failure(AsyncEndpointError.FromException(ex));
        }
    }
    
    private async Task ProcessDelayedJobs(CancellationToken cancellationToken)
    {
        // Move all delayed jobs that are now eligible to the active queue
        var now = _dateTimeProvider.DateTimeOffsetNow.ToUnixTimeSeconds();
        
        var eligibleJobIds = await _database.SortedSetRangeByScoreAsync(
            _delayedQueueKey,
            start: double.NegativeInfinity,
            stop: now
        );
        
        if (eligibleJobIds.Length == 0) return;

        // Process each eligible job
        foreach (var jobIdString in eligibleJobIds)
        {
            if (!Guid.TryParse(jobIdString, out var jobId))
                continue;

            // Remove from delayed queue
            await _database.SortedSetRemoveAsync(_delayedQueueKey, jobIdString);
            
            // Add to active stream to make it available for processing
            var streamEntry = new NameValueEntry[]
            {
                new NameValueEntry("jobId", jobIdString),
                new NameValueEntry("status", ((int)JobStatus.Queued).ToString()),
                new NameValueEntry("timestamp", now.ToString()),
                new NameValueEntry("madeAvailable", _dateTimeProvider.DateTimeOffsetNow.ToString("O"))
            };
            
            await _database.StreamAddAsync(_activeStreamKey, streamEntry);
        }
    }
    
    public async Task<int> RecoverStuckJobs(long timeoutUnixTime, int maxRetries, CancellationToken cancellationToken)
    {
        // Implementation for recovering stuck jobs
        // This needs to handle both active stream and delayed queue
        var luaScript = @"
            local timeoutUnixTime = tonumber(ARGV[1])
            local maxRetries = tonumber(ARGV[2])
            local currentTimeUnix = tonumber(ARGV[3])
            local currentTimeIso = ARGV[4]
            local inProgressStatus = tonumber(ARGV[5])
            local scheduledStatus = tonumber(ARGV[6])
            local failedStatus = tonumber(ARGV[7])
            local inProgressKey = ARGV[8]
            
            -- Get all in-progress jobs from the stream's PEL (Pending Entries List)
            local pendingEntries = redis.call('XPENDING', inProgressKey, 'workers')
            
            local recoveredCount = 0
            
            for _, entry in ipairs(pendingEntries) do
                local entryId = entry['id']
                local consumer = entry['consumer']
                local idleTime = entry['idle-time']
                local deliveryCount = entry['delivery-count']
                local jobId = 'unknown'
                
                -- Get the actual job data from the stream entry
                local streamEntries = redis.call('XRANGE', inProgressKey, entryId, entryId)
                if streamEntries and #streamEntries > 0 then
                    local fields = streamEntries[1]['field']
                    for i = 1, #fields, 2 do
                        if fields[i] == 'jobId' then
                            jobId = fields[i + 1]
                            break
                        end
                    end
                end
                
                -- Check if the job exists and has an in-progress status
                if jobId ~= 'unknown' then
                    local jobKey = 'ae:job:' .. jobId
                    local status = redis.call('HGET', jobKey, 'Status')
                    local startedAtUnix = redis.call('HGET', jobKey, 'StartedAtUnix')
                    local retryCount = redis.call('HGET', jobKey, 'RetryCount') or '0'
                    local maxRetriesForJob = redis.call('HGET', jobKey, 'MaxRetries') or ARGV[2]

                    -- Check that job is in-progress and has a valid start time before timeout
                    if tonumber(status) == inProgressStatus and startedAtUnix and startedAtUnix ~= '' and tonumber(startedAtUnix) <= timeoutUnixTime then
                        retryCount = tonumber(retryCount)
                        maxRetriesForJob = tonumber(maxRetriesForJob)

                        if retryCount < maxRetriesForJob then
                            -- Recover: reschedule immediately, increment retry count
                            local newRetryCount = retryCount + 1

                            redis.call('HSET', jobKey,
                                'Status', tostring(scheduledStatus),
                                'RetryCount', tostring(newRetryCount),
                                'RetryDelayUntil', '',
                                'WorkerId', '',
                                'StartedAt', '',
                                'StartedAtUnix', '',
                                'LastUpdatedAt', currentTimeIso)

                            -- Add back to delayed queue with immediate availability (0 score)
                            redis.call('ZADD', 'ae:jobs:delayed', currentTimeUnix, jobId)
                            
                            -- Acknowledge the pending entry to remove it from PEL
                            redis.call('XACK', inProgressKey, 'workers', entryId)
                            
                            recoveredCount = recoveredCount + 1
                        else
                            -- Mark as permanently failed
                            redis.call('HSET', jobKey,
                                'Status', tostring(failedStatus),
                                'Error', 'Job failed after maximum retries',
                                'WorkerId', '',
                                'StartedAt', '',
                                'StartedAtUnix', '',
                                'LastUpdatedAt', currentTimeIso)

                            -- Acknowledge the pending entry
                            redis.call('XACK', inProgressKey, 'workers', entryId)
                        end
                    end
                end
            end

            return recoveredCount
        ";

        var now = _dateTimeProvider.DateTimeOffsetNow;
        var currentTimeUnix = now.ToUnixTimeSeconds();
        var currentTimeIso = now.ToString("O"); // ISO 8601 format

        var result = await database.ScriptEvaluateAsync(luaScript,
            values: [
                timeoutUnixTime.ToString(),
                maxRetries.ToString(),
                currentTimeUnix.ToString(),
                currentTimeIso,
                ((int)JobStatus.InProgress).ToString(),
                ((int)JobStatus.Scheduled).ToString(),
                ((int)JobStatus.Failed).ToString(),
                _inProgressStreamKey  // ARGV[8] - in-progress stream key
            ]);

        return (int)(long)result;
    }
}
```

### Approach 2: Background Scheduler Service

Create a dedicated background service to handle delayed execution:

```csharp
public class DelayedJobSchedulerService : BackgroundService
{
    private readonly IJobStore _jobStore;
    private readonly IDatabase _database;
    private readonly ILogger<DelayedJobSchedulerService> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly string _delayedQueueKey = "ae:jobs:delayed";
    private readonly string _activeStreamKey = "ae:jobs:stream";
    
    public DelayedJobSchedulerService(
        IJobStore jobStore,
        IDatabase database,
        ILogger<DelayedJobSchedulerService> logger,
        IDateTimeProvider dateTimeProvider)
    {
        _jobStore = jobStore;
        _database = database;
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Delayed Job Scheduler Service starting");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDelayedJobs();
                
                // Wait 1 second between checks
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in delayed job scheduler");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // Backoff on error
            }
        }
    }
    
    private async Task ProcessDelayedJobs()
    {
        var now = _dateTimeProvider.DateTimeOffsetNow.ToUnixTimeSeconds();
        
        // Get eligible delayed jobs with a small buffer (e.g., jobs delayed to execute in next 5 seconds too)
        var eligibleJobIds = await _database.SortedSetRangeByScoreAsync(
            _delayedQueueKey,
            start: double.NegativeInfinity,
            stop: now + 5 // Small buffer to account for processing time
        );
        
        if (eligibleJobIds.Length == 0) return;
        
        foreach (var jobIdString in eligibleJobIds)
        {
            if (!Guid.TryParse(jobIdString, out var jobId))
                continue;
                
            // Remove from delayed queue
            var removed = await _database.SortedSetRemoveAsync(_delayedQueueKey, jobIdString);
            if (!removed) continue;
            
            // Get the job and verify it should be executed now
            var jobResult = await _jobStore.GetJobById(jobId, CancellationToken.None);
            if (!jobResult.IsSuccess || jobResult.Data == null) continue;
            
            var job = jobResult.Data;
            
            // Only move to active if it's still scheduled and delay time has passed
            if ((job.Status == JobStatus.Scheduled || job.Status == JobStatus.Queued) && 
                (!job.RetryDelayUntil.HasValue || job.RetryDelayUntil <= _dateTimeProvider.DateTimeOffsetNow))
            {
                // Update job status to queued
                job.UpdateStatus(JobStatus.Queued, _dateTimeProvider);
                job.RetryDelayUntil = null;
                
                var updateResult = await _jobStore.UpdateJob(job, CancellationToken.None);
                if (updateResult.IsSuccess)
                {
                    // Add to active stream to make it available for workers
                    var streamEntry = new NameValueEntry[]
                    {
                        new NameValueEntry("jobId", jobIdString),
                        new NameValueEntry("status", ((int)JobStatus.Queued).ToString()),
                        new NameValueEntry("timestamp", _dateTimeProvider.DateTimeOffsetNow.ToUnixTimeSeconds().ToString())
                    };
                    
                    await _database.StreamAddAsync(_activeStreamKey, streamEntry);
                    _logger.LogDebug("Moved delayed job {JobId} to active queue", jobId);
                }
            }
        }
    }
}
```

### Approach 3: Time-Bucket System

Use time-bucket approach to group delayed jobs by execution time:

```csharp
public class TimeBucketRedisJobStore : IJobStore
{
    private readonly string _activeStreamKey = "ae:jobs:stream";
    private readonly string _bucketPrefix = "ae:jobs:bucket:";
    private readonly int _bucketSizeMinutes = 5; // Group jobs into 5-minute buckets
    private readonly ITimeBucketCalculator _timeBucketCalculator;
    
    private long GetBucketKey(DateTimeOffset executionTime)
    {
        var minutesFromEpoch = (long)(executionTime.ToUnixTimeSeconds() / 60);
        var bucketStartMinute = (minutesFromEpoch / _bucketSizeMinutes) * _bucketSizeMinutes;
        return bucketStartMinute;
    }
    
    public async Task<MethodResult> CreateJob(Job job, CancellationToken cancellationToken)
    {
        var jobKey = GetJobKey(job.Id);
        var hashEntries = _jobHashConverter.ConvertToHashEntries(job);
        await _database.HashSetAsync(jobKey, hashEntries);

        if (job.RetryDelayUntil.HasValue && job.RetryDelayUntil > _dateTimeProvider.UtcNow)
        {
            // Add job to the appropriate time bucket
            var bucketKey = _bucketPrefix + GetBucketKey(job.RetryDelayUntil.Value);
            
            var listEntry = new HashEntry[]
            {
                new HashEntry("jobId", job.Id.ToString()),
                new HashEntry("delayUntil", job.RetryDelayUntil.Value.ToUnixTimeSeconds().ToString()),
                new HashEntry("addedAt", _dateTimeProvider.DateTimeOffsetNow.ToUnixTimeSeconds().ToString())
            };
            
            await _database.HashSetAsync(bucketKey, listEntry);
            
            // Add bucket key to a sorted set to track when buckets become active
            var bucketsListKey = "ae:jobs:buckets:list";
            var bucketScore = job.RetryDelayUntil.Value.ToUnixTimeSeconds();
            await _database.SortedSetAddAsync(bucketsListKey, bucketKey, bucketScore);
        }
        else
        {
            // Add to active stream immediately
            var streamEntry = new NameValueEntry[]
            {
                new NameValueEntry("jobId", job.Id.ToString()),
                new NameValueEntry("status", ((int)job.Status).ToString()),
                new NameValueEntry("timestamp", _dateTimeProvider.DateTimeOffsetNow.ToUnixTimeSeconds().ToString())
            };
            await _database.StreamAddAsync(_activeStreamKey, streamEntry);
        }
        
        return MethodResult.Success();
    }
    
    // Background service would process expired buckets and move jobs to active stream
    private async Task ProcessExpiredBuckets()
    {
        var now = _dateTimeProvider.DateTimeOffsetNow.ToUnixTimeSeconds();
        
        var expiredBucketKeys = await _database.SortedSetRangeByScoreAsync(
            "ae:jobs:buckets:list",
            start: double.NegativeInfinity,
            stop: now
        );
        
        foreach (var bucketKey in expiredBucketKeys)
        {
            // Process all jobs in this bucket
            var jobHashes = await _database.HashGetAllAsync(bucketKey);
            foreach (var jobHash in jobHashes)
            {
                // Move job to active stream
                await _database.StreamAddAsync(_activeStreamKey, new NameValueEntry[]
                {
                    new NameValueEntry("jobId", jobHash.Value),
                    new NameValueEntry("status", ((int)JobStatus.Queued).ToString()),
                    new NameValueEntry("timestamp", now.ToString())
                });
            }
            
            // Remove the processed bucket
            await _database.KeyDeleteAsync(bucketKey);
            await _database.SortedSetRemoveAsync("ae:jobs:buckets:list", bucketKey);
        }
    }
}
```

## Recommendation

**Approach 1 (Dual Queue System)** is the recommended solution because it:

1. **Maintains Performance**: Uses Redis Streams for active job processing while handling delays with sorted sets
2. **Preserves Existing Logic**: Most of the existing delay handling logic can be reused
3. **Ensures Reliability**: Combines the best features of both data structures
4. **Enables Consumer Groups**: Maintains the benefits of Redis Streams for job distribution
5. **Handles Recovery**: Both active and delayed jobs can be properly recovered
6. **Minimizes Complexity**: Doesn't require significant changes to the core architecture

The dual approach effectively splits the concern: Sorted Sets handle the temporal aspect (when to execute), while Redis Streams handle the processing aspect (how to distribute and process).

## Implementation Benefits

1. **Better Performance**: O(1) for active jobs vs O(log N) for delayed jobs
2. **Scalability**: Redis Streams handle high-throughput active jobs efficiently
3. **Reliability**: Built-in acknowledgment and consumer groups for active jobs
4. **Flexibility**: Can tune the balance between stream and sorted set usage
5. **Backward Compatibility**: Existing delay logic can be adapted with minimal changes