# Hash-Based Redis Job Store Architecture

## Overview

This document outlines a clean architecture for implementing the hash-based Redis job store with proper dependency management and separation of concerns.

## Recommended Architecture

### 1. JobHashConverter Service

First, let's create a dedicated service for converting between Job objects and Redis hash entries:

```csharp
public interface IJobHashConverter
{
    HashEntry[] ConvertToHashEntries(Job job);
    Job ConvertFromHashEntries(HashEntry[] hashEntries);
}

public class JobHashConverter : IJobHashConverter
{
    private readonly ISerializer _serializer;

    public JobHashConverter(ISerializer serializer)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public HashEntry[] ConvertToHashEntries(Job job)
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

    public Job ConvertFromHashEntries(HashEntry[] hashEntries)
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
}
```

### 2. Updated RedisJobStore Implementation

Now the RedisJobStore can be updated to use the converter service:

```csharp
public class RedisJobStore : IJobStore
{
    private readonly ILogger<RedisJobStore> _logger;
    private readonly IDatabase _database;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IJobHashConverter _jobHashConverter;
    private readonly string? _connectionString;

    private static readonly string _queueKey = "ae:jobs:queue";
    private static readonly DateTime _unixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public RedisJobStore(
        ILogger<RedisJobStore> logger, 
        string connectionString, 
        IDateTimeProvider dateTimeProvider, 
        IJobHashConverter jobHashConverter)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        _jobHashConverter = jobHashConverter ?? throw new ArgumentNullException(nameof(jobHashConverter));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _database = InitializeDatabase(_connectionString);
    }

    public RedisJobStore(
        ILogger<RedisJobStore> logger, 
        IDatabase database, 
        IDateTimeProvider dateTimeProvider, 
        IJobHashConverter jobHashConverter)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        _jobHashConverter = jobHashConverter ?? throw new ArgumentNullException(nameof(jobHashConverter));
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    // CreateJob method
    public async Task<MethodResult> CreateJob(Job job, CancellationToken cancellationToken)
    {
        try
        {
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

            var jobExists = await _database.KeyExistsAsync(jobKey);
            if (jobExists)
            {
                _logger.LogError("Job with ID {JobId} already exists", job.Id);
                return MethodResult.Failure(
                    AsyncEndpointError.FromCode("JOB_CREATE_FAILED", $"Job with ID {job.Id} already exists"));
            }

            var hashEntries = _jobHashConverter.ConvertToHashEntries(job);
            var created = await _database.HashSetAsync(jobKey, hashEntries, When.NotExists);

            if (!created)
            {
                _logger.LogError("Failed to create job with ID {JobId}", job.Id);
                return MethodResult.Failure(
                    AsyncEndpointError.FromCode("JOB_CREATE_FAILED", $"Failed to create job with ID {job.Id}"));
            }

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

    // GetJobById method
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
            var hashEntries = await _database.HashGetAllAsync(jobKey);

            if (hashEntries.Length == 0)
            {
                _logger.LogWarning("Job not found with Id {JobId} from store", id);
                return MethodResult<Job>.Failure(
                    AsyncEndpointError.FromCode("JOB_NOT_FOUND", $"Job with ID {id} not found"));
            }

            var job = _jobHashConverter.ConvertFromHashEntries(hashEntries);
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

    // UpdateJob method
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

            job.LastUpdatedAt = _dateTimeProvider.DateTimeOffsetNow;
            var hashEntries = _jobHashConverter.ConvertToHashEntries(job);
            await _database.HashSetAsync(jobKey, hashEntries);

            await _database.SortedSetRemoveAsync(_queueKey, job.Id.ToString());

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

    // ClaimSingleJob method with updated approach
    private async Task<MethodResult<Job>> ClaimSingleJob(Guid jobId, Guid workerId, CancellationToken cancellationToken)
    {
        var jobKey = GetJobKey(jobId);

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

        if (result.IsNull || result.ToString().StartsWith("NOSCRIPT"))
        {
            return MethodResult<Job>.Failure(AsyncEndpointError.FromCode("JOB_CLAIM_ERROR", "Could not claim job due to script error"));
        }

        try
        {
            var resultArray = (RedisValue[])result;
            var claimedJob = new Job
            {
                Id = Guid.Parse(resultArray[0].ToString()),
                Name = resultArray[1].ToString(),
                Status = (JobStatus)int.Parse(resultArray[2].ToString()),
                Headers = string.IsNullOrEmpty(resultArray[3].ToString()) ? 
                          new Dictionary<string, List<string?>>() : 
                          _serializer.Deserialize<Dictionary<string, List<string?>>>(resultArray[3].ToString()),
                RouteParams = string.IsNullOrEmpty(resultArray[4].ToString()) ? 
                              new Dictionary<string, object?>() : 
                              _serializer.Deserialize<Dictionary<string, object?>>(resultArray[4].ToString()),
                QueryParams = string.IsNullOrEmpty(resultArray[5].ToString()) ? 
                              new List<KeyValuePair<string, List<string?>>>() : 
                              _serializer.Deserialize<List<KeyValuePair<string, List<string?>>>>(resultArray[5].ToString()),
                Payload = resultArray[6].ToString(),
                Result = string.IsNullOrEmpty(resultArray[7].ToString()) ? null : resultArray[7].ToString(),
                Error = string.IsNullOrEmpty(resultArray[8].ToString()) ? null : 
                        _serializer.Deserialize<AsyncEndpointError>(resultArray[8].ToString()),
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
    
    // Other helper methods (InitializeDatabase, GetJobKey, GetJobScore, etc.) remain the same
    
    private IDatabase InitializeDatabase(string connectionString)
    {
        try
        {
            var redis = ConnectionMultiplexer.Connect(connectionString);

            redis.ConnectionFailed += (sender, e) =>
                _logger.LogError(e.Exception, "Redis connection failed: {ErrorMessage}", e.Exception?.Message);
            redis.ConnectionRestored += (sender, e) =>
                _logger.LogInformation("Redis connection restored");

            return redis.GetDatabase();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to connect to Redis with connection string: {ConnectionString}", connectionString);
            throw;
        }
    }

    private static string GetJobKey(Guid jobId) => $"ae:job:{jobId}";

    private static double GetJobScore(Job job)
    {
        var effectiveTime = job.RetryDelayUntil ?? job.CreatedAt.UtcDateTime;
        return GetScoreForTime(effectiveTime);
    }

    private static double GetScoreForTime(DateTime dateTime)
    {
        return (dateTime - _unixEpoch).TotalSeconds;
    }
}
```

### 3. DI Registration

Finally, add the service to the DI container:

```csharp
// In your service registration
services.AddSingleton<IJobHashConverter, JobHashConverter>();
services.AddSingleton<IJobStore, RedisJobStore>();
```

## Benefits of This Architecture

1. **Separation of Concerns**: Serialization/deserialization logic is separated into its own service
2. **Testability**: Each component can be tested independently
3. **Maintainability**: Changes to serialization logic only affect the converter service
4. **Reusability**: The converter service can be used elsewhere if needed
5. **Clean Code**: RedisJobStore focuses only on Redis operations