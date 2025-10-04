# Hash-Based Redis Job Store Implementation

## Overview

This document details the changes required to convert the current Redis job storage from string-based JSON serialization to hash-based storage for improved performance and efficiency.

## Key Changes Required

### 1. Data Structure Changes

#### Current Structure
```
ae:job:{jobId} -> STRING (JSON serialized job object)
```

#### New Structure
```
ae:job:{jobId} -> HASH
  - Id: {Guid}
  - Name: {string}
  - Status: {int} (JobStatus enum value)
  - Headers: {JSON}
  - RouteParams: {JSON}
  - QueryParams: {JSON}
  - Payload: {string}
  - Result: {string}
  - Error: {JSON}
  - RetryCount: {int}
  - MaxRetries: {int}
  - RetryDelayUntil: {timestamp} (ISO string format)
  - WorkerId: {Guid} (nullable)
  - CreatedAt: {ISO date string}
  - StartedAt: {ISO date string} (nullable)
  - CompletedAt: {ISO date string} (nullable)
  - LastUpdatedAt: {ISO date string}
```

### 2. Required Methods

#### Helper Methods for Hash Conversion

```csharp
private static HashEntry[] JobToHashEntries(Job job)
{
    return new[]
    {
        new HashEntry(nameof(Job.Id), job.Id.ToString()),
        new HashEntry(nameof(Job.Name), job.Name),
        new HashEntry(nameof(Job.Status), (int)job.Status),
        new HashEntry(nameof(Job.Headers), Serialize(job.Headers)),
        new HashEntry(nameof(Job.RouteParams), Serialize(job.RouteParams)),
        new HashEntry(nameof(Job.QueryParams), Serialize(job.QueryParams)),
        new HashEntry(nameof(Job.Payload), job.Payload),
        new HashEntry(nameof(Job.Result), job.Result ?? ""),
        new HashEntry(nameof(Job.Error), job.Error != null ? Serialize(job.Error) : ""),
        new HashEntry(nameof(Job.RetryCount), job.RetryCount),
        new HashEntry(nameof(Job.MaxRetries), job.MaxRetries),
        new HashEntry(nameof(Job.RetryDelayUntil), job.RetryDelayUntil?.ToString("O") ?? ""),
        new HashEntry(nameof(Job.WorkerId), job.WorkerId?.ToString() ?? ""),
        new HashEntry(nameof(Job.CreatedAt), job.CreatedAt.ToString("O")),
        new HashEntry(nameof(Job.StartedAt), job.StartedAt?.ToString("O") ?? ""),
        new HashEntry(nameof(Job.CompletedAt), job.CompletedAt?.ToString("O") ?? ""),
        new HashEntry(nameof(Job.LastUpdatedAt), job.LastUpdatedAt.ToString("O"))
    };
}

private static Job HashToJob(HashEntry[] hashEntries)
{
    var dict = hashEntries.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

    return new Job
    {
        Id = Guid.Parse(dict[nameof(Job.Id)]),
        Name = dict[nameof(Job.Name)],
        Status = (JobStatus)int.Parse(dict[nameof(Job.Status)]),
        Headers = string.IsNullOrEmpty(dict[nameof(Job.Headers)]) ? new Dictionary<string, List<string?>>() : Deserialize<Dictionary<string, List<string?>>>(dict[nameof(Job.Headers)]),
        RouteParams = string.IsNullOrEmpty(dict[nameof(Job.RouteParams)]) ? new Dictionary<string, object?>() : Deserialize<Dictionary<string, object?>>(dict[nameof(Job.RouteParams)]),
        QueryParams = string.IsNullOrEmpty(dict[nameof(Job.QueryParams)]) ? new List<KeyValuePair<string, List<string?>>>() : Deserialize<List<KeyValuePair<string, List<string?>>>>(dict[nameof(Job.QueryParams)]),
        Payload = dict[nameof(Job.Payload)],
        Result = string.IsNullOrEmpty(dict[nameof(Job.Result)]) ? null : dict[nameof(Job.Result)],
        Error = string.IsNullOrEmpty(dict[nameof(Job.Error)]) ? null : Deserialize<AsyncEndpointError>(dict[nameof(Job.Error)]),
        RetryCount = int.Parse(dict[nameof(Job.RetryCount)]),
        MaxRetries = int.Parse(dict[nameof(Job.MaxRetries)]),
        RetryDelayUntil = string.IsNullOrEmpty(dict[nameof(Job.RetryDelayUntil)]) ? null : DateTime.Parse(dict[nameof(Job.RetryDelayUntil)]),
        WorkerId = string.IsNullOrEmpty(dict[nameof(Job.WorkerId)]) ? null : Guid.Parse(dict[nameof(Job.WorkerId)]),
        CreatedAt = DateTimeOffset.Parse(dict[nameof(Job.CreatedAt)]),
        StartedAt = string.IsNullOrEmpty(dict[nameof(Job.StartedAt)]) ? null : DateTimeOffset.Parse(dict[nameof(Job.StartedAt)]),
        CompletedAt = string.IsNullOrEmpty(dict[nameof(Job.CompletedAt)]) ? null : DateTimeOffset.Parse(dict[nameof(Job.CompletedAt)]),
        LastUpdatedAt = DateTimeOffset.Parse(dict[nameof(Job.LastUpdatedAt)])
    };
}

private string Serialize(object obj)
{
    if (obj == null) return "";
    return _serializer.Serialize(obj);
}

private T Deserialize<T>(string value)
{
    if (string.IsNullOrEmpty(value)) return default(T);
    return _serializer.Deserialize<T>(value);
}
```

### 3. Method-by-Method Changes

#### CreateJob Method
```csharp
public async Task<MethodResult> CreateJob(Job job, CancellationToken cancellationToken)
{
    try
    {
        // Validation code remains the same
        if (job == null)
        {
            _logger.LogWarning("Attempted to create null job");
            return MethodResult.Failure(
                AsyncEndpointError.FromCode("INVALID_JOB", "Job cannot be null"));
        }

        if (job.Id == Guid.Empty)
        {
            _logger.LogWarning("Attempted to create job with empty ID");
            return MethodResult.Failure(
                AsyncEndpointError.FromCode("INVALID_JOB_ID", "Job ID cannot be empty"));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Job create operation cancelled");
            return await Task.FromCanceled<MethodResult>(cancellationToken);
        }

        var jobKey = GetJobKey(job.Id);

        // Use hash exists check to avoid overwriting
        var jobExists = await _database.KeyExistsAsync(jobKey);
        if (jobExists)
        {
            _logger.LogError("Job with ID {JobId} already exists", job.Id);
            return MethodResult.Failure(
                AsyncEndpointError.FromCode("JOB_CREATE_FAILED", $"Job with ID {job.Id} already exists"));
        }

        // Create job in Redis as a hash
        var hashEntries = JobToHashEntries(job);
        var created = await _database.HashSetAsync(jobKey, hashEntries, When.NotExists);

        if (!created)
        {
            _logger.LogError("Failed to create job with ID {JobId}", job.Id);
            return MethodResult.Failure(
                AsyncEndpointError.FromCode("JOB_CREATE_FAILED", $"Failed to create job with ID {job.Id}"));
        }

        // Add job to the queue set if it's queued
        if (job.Status == JobStatus.Queued)
        {
            await _database.SortedSetAddAsync(_queueKey, job.Id.ToString(), GetJobScore(job));
        }

        _logger.LogInformation("Created job {JobId} with name {JobName}", job.Id, job.Name);
        return MethodResult.Success();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error creating job: {JobName}", job?.Name);
        return MethodResult.Failure(
            AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error creating job: {ex.Message}", ex));
    }
}
```

#### GetJobById Method
```csharp
public async Task<MethodResult<Job>> GetJobById(Guid id, CancellationToken cancellationToken)
{
    try
    {
        if (id == Guid.Empty)
        {
            _logger.LogWarning("Attempted to retrieve job with empty ID");
            return MethodResult<Job>.Failure(
                AsyncEndpointError.FromCode("INVALID_JOB_ID", "Job ID cannot be empty"));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Job get operation cancelled for ID {JobId}", id);
            return await Task.FromCanceled<MethodResult<Job>>(cancellationToken);
        }

        var jobKey = GetJobKey(id);
        
        // Get all hash fields
        var hashEntries = await _database.HashGetAllAsync(jobKey);

        if (hashEntries.Length == 0)
        {
            _logger.LogWarning("Job not found with Id {JobId} from store", id);
            return MethodResult<Job>.Failure(
                AsyncEndpointError.FromCode("JOB_NOT_FOUND", $"Job with ID {id} not found"));
        }

        var job = HashToJob(hashEntries);
        if (job == null)
        {
            _logger.LogError("Conversion failed for job with ID {JobId}", id);
            return MethodResult<Job>.Failure(
                AsyncEndpointError.FromCode("DESERIALIZATION_ERROR", $"Failed to convert hash to job with ID {id}"));
        }

        return MethodResult<Job>.Success(job);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error retrieving job: {JobId}", id);
        return MethodResult<Job>.Failure(
            AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error retrieving job: {ex.Message}", ex));
    }
}
```

#### UpdateJob Method
```csharp
public async Task<MethodResult> UpdateJob(Job job, CancellationToken cancellationToken)
{
    try
    {
        if (job == null)
        {
            _logger.LogWarning("Attempted to update null job");
            return MethodResult.Failure(
                AsyncEndpointError.FromCode("INVALID_JOB", "Job cannot be null"));
        }

        if (job.Id == Guid.Empty)
        {
            _logger.LogWarning("Attempted to update job with empty ID");
            return MethodResult.Failure(
                AsyncEndpointError.FromCode("INVALID_JOB_ID", "Job ID cannot be empty"));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Job update operation cancelled for ID {JobId}", job.Id);
            return await Task.FromCanceled<MethodResult>(cancellationToken);
        }

        var jobKey = GetJobKey(job.Id);
        var jobExists = await _database.KeyExistsAsync(jobKey);

        if (!jobExists)
        {
            _logger.LogWarning("Attempted to update non-existent job {JobId}", job.Id);
            return MethodResult.Failure(
                AsyncEndpointError.FromCode("JOB_NOT_FOUND", $"Job with ID {job.Id} not found"));
        }

        // Update the last updated timestamp
        job.LastUpdatedAt = _dateTimeProvider.DateTimeOffsetNow;
        
        // Convert the job to hash entries and update the hash
        var hashEntries = JobToHashEntries(job);
        await _database.HashSetAsync(jobKey, hashEntries);

        // Update queue if job status has changed
        await _database.SortedSetRemoveAsync(_queueKey, job.Id.ToString());

        // Only add back to queue if it's queued or scheduled for retry
        if (job.Status == JobStatus.Queued ||
            job.Status == JobStatus.Scheduled && (job.RetryDelayUntil == null || job.RetryDelayUntil <= _dateTimeProvider.UtcNow))
        {
            await _database.SortedSetAddAsync(_queueKey, job.Id.ToString(), GetJobScore(job));
        }

        _logger.LogDebug("Updated job {JobId}", job.Id);
        return MethodResult.Success();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error updating job: {JobId}", job?.Id);
        return MethodResult.Failure(
            AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error updating job: {ex.Message}", ex));
    }
}
```

#### ClaimSingleJob Method (Critical Section - Optimized)
For the ClaimSingleJob method, we'll use an atomic Lua script that handles all validation and updating in one operation, without pre-fetching the job:

```csharp
private async Task<MethodResult<Job>> ClaimSingleJob(Guid jobId, Guid workerId, CancellationToken cancellationToken)
{
    var jobKey = GetJobKey(jobId);

    // Use atomic Lua script to check and claim the job in one operation
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

        -- Get required fields atomically
        local currentStatus = redis.call('HGET', jobKey, 'Status')
        local currentWorkerId = redis.call('HGET', jobKey, 'WorkerId')
        local currentRetryDelayUntil = redis.call('HGET', jobKey, 'RetryDelayUntil')

        -- Check if job can be claimed - all checks in one atomic operation
        if currentWorkerId and currentWorkerId ~= '' then
            return redis.error_reply('ALREADY_ASSIGNED')
        end

        if not (currentStatus == expectedStatus1 or currentStatus == expectedStatus2) then
            return redis.error_reply('WRONG_STATUS')
        end

        -- Check retry delay if it exists
        if currentRetryDelayUntil and currentRetryDelayUntil ~= '' then
            local retryUntil = tonumber(currentRetryDelayUntil)
            if retryUntil and retryUntil > tonumber(currentTime) then
                return redis.error_reply('RETRY_DELAY')
            end
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

        -- Claim the job atomically
        redis.call('HSET', jobKey, 'Status', newStatus)
        redis.call('HSET', jobKey, 'WorkerId', newWorkerId)
        redis.call('HSET', jobKey, 'StartedAt', newStartedAt)
        redis.call('HSET', jobKey, 'LastUpdatedAt', newLastUpdatedAt)
        redis.call('ZREM', queueKey, jobId)

        -- Return all fields needed to construct the complete job object
        return { 
            currentId, currentName, newStatus, currentHeaders, currentRouteParams, 
            currentQueryParams, currentPayload, currentResult, currentError, 
            currentRetryCount, currentMaxRetries, currentRetryDelayUntil, 
            currentWorkerId, currentCreatedAt, newStartedAt, currentCompletedAt, newLastUpdatedAt
        }
    ";

    var now = _dateTimeProvider.DateTimeOffsetNow;
    var currentTime = now.ToUnixTimeSeconds().ToString();

    var result = await _database.ScriptEvaluateAsync(
        luaScript,
        values: new RedisValue[]
        {
            jobKey,
            ((int)JobStatus.Queued).ToString(),      // Expected status 1
            ((int)JobStatus.Scheduled).ToString(),   // Expected status 2
            ((int)JobStatus.InProgress).ToString(),  // New status
            workerId.ToString(),                     // New worker ID
            now.ToString("O"),                       // Started at
            now.ToString("O"),                       // Last updated at
            _queueKey,
            jobId.ToString(),
            currentTime                              // Current time for retry delay check
        }
    );

    // Handle the script result
    if (result.IsNull || result.ToString().StartsWith("NOSCRIPT"))
    {
        // Lua script error occurred
        return MethodResult<Job>.Failure(AsyncEndpointError.FromCode("JOB_CLAIM_ERROR", "Could not claim job due to script error"));
    }

    try
    {
        // Construct the job from the returned values
        var resultArray = (RedisValue[])result;
        var claimedJob = new Job
        {
            Id = Guid.Parse(resultArray[0].ToString()),
            Name = resultArray[1].ToString(),
            Status = (JobStatus)int.Parse(resultArray[2].ToString()),
            Headers = string.IsNullOrEmpty(resultArray[3].ToString()) ? 
                      new Dictionary<string, List<string?>>() : 
                      Deserialize<Dictionary<string, List<string?>>>(resultArray[3].ToString()),
            RouteParams = string.IsNullOrEmpty(resultArray[4].ToString()) ? 
                          new Dictionary<string, object?>() : 
                          Deserialize<Dictionary<string, object?>>(resultArray[4].ToString()),
            QueryParams = string.IsNullOrEmpty(resultArray[5].ToString()) ? 
                          new List<KeyValuePair<string, List<string?>>>() : 
                          Deserialize<List<KeyValuePair<string, List<string?>>>>(resultArray[5].ToString()),
            Payload = resultArray[6].ToString(),
            Result = string.IsNullOrEmpty(resultArray[7].ToString()) ? null : resultArray[7].ToString(),
            Error = string.IsNullOrEmpty(resultArray[8].ToString()) ? null : 
                    Deserialize<AsyncEndpointError>(resultArray[8].ToString()),
            RetryCount = int.Parse(resultArray[9].ToString()),
            MaxRetries = int.Parse(resultArray[10].ToString()),
            RetryDelayUntil = string.IsNullOrEmpty(resultArray[11].ToString()) ? null : 
                             DateTime.Parse(resultArray[11].ToString()),
            WorkerId = workerId, // Newly assigned
            CreatedAt = DateTimeOffset.Parse(resultArray[12].ToString()),
            StartedAt = DateTimeOffset.Parse(resultArray[13].ToString()), // Newly set
            CompletedAt = string.IsNullOrEmpty(resultArray[14].ToString()) ? null : DateTimeOffset.Parse(resultArray[14].ToString()),
            LastUpdatedAt = DateTimeOffset.Parse(resultArray[15].ToString()) // Newly set
        };

        return MethodResult<Job>.Success(claimedJob);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error constructing job object after claiming job {JobId}", jobId);
        return MethodResult<Job>.Failure(AsyncEndpointError.FromCode("JOB_CONSTRUCTION_ERROR", $"Error constructing job object: {ex.Message}"));
    }
}
```

### 4. Testing Considerations

- Update all existing unit tests to work with the new hash-based implementation
- Ensure all atomic operations still work correctly with the new hash-based Lua scripts
- Test for edge cases like null values and proper serialization/deserialization

This implementation provides significant performance improvements by reducing network traffic, avoiding unnecessary serialization/deserialization, and allowing atomic operations on specific job properties while maintaining the same external API.