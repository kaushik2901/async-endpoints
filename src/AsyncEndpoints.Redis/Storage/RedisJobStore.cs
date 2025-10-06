using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.Infrastructure.Serialization;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AsyncEndpoints.Redis.Storage;

/// <summary>
/// A Redis-based implementation of the IJobStore interface.
/// This implementation stores jobs in Redis and is suitable for distributed deployments.
/// </summary>
public class RedisJobStore : IJobStore
{
	private readonly ILogger<RedisJobStore> _logger;
	private readonly IDatabase _database;
	private readonly IDateTimeProvider _dateTimeProvider;
	private readonly IJobHashConverter _jobHashConverter;
	private readonly ISerializer _serializer;
	private readonly string? _connectionString;

	private static readonly string _queueKey = "ae:jobs:queue";
	private static readonly DateTime _unixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

	public bool SupportsJobRecovery => true; // Redis supports recovery

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisJobStore"/> class.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	/// <param name="connectionString">The Redis connection string.</param>
	/// <param name="dateTimeProvider">Provider for current date and time.</param>
	/// <param name="jobHashConverter">The job hash converter service.</param>
	/// <param name="serializer">The serializer service.</param>
	public RedisJobStore(ILogger<RedisJobStore> logger, string connectionString, IDateTimeProvider dateTimeProvider, IJobHashConverter jobHashConverter, ISerializer serializer)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
		_jobHashConverter = jobHashConverter ?? throw new ArgumentNullException(nameof(jobHashConverter));
		_serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
		_connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
		_database = InitializeDatabase(_connectionString);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisJobStore"/> class with a pre-configured database.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	/// <param name="database">The Redis database instance.</param>
	/// <param name="dateTimeProvider">Provider for current date and time.</param>
	/// <param name="jobHashConverter">The job hash converter service.</param>
	/// <param name="serializer">The serializer service.</param>
	public RedisJobStore(ILogger<RedisJobStore> logger, IDatabase database, IDateTimeProvider dateTimeProvider, IJobHashConverter jobHashConverter, ISerializer serializer)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
		_jobHashConverter = jobHashConverter ?? throw new ArgumentNullException(nameof(jobHashConverter));
		_serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
		_database = database ?? throw new ArgumentNullException(nameof(database));
	}

	/// <summary>
	/// Creates a new job in the store.
	/// </summary>
	/// <param name="job">The job to create.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A <see cref="MethodResult"/> indicating the result of the operation.</returns>
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

			// Use hash exists check to avoid overwriting
			var jobExists = await _database.KeyExistsAsync(jobKey);
			if (jobExists)
			{
				_logger.LogError("Job with ID {JobId} already exists", job.Id);
				return MethodResult.Failure(
					AsyncEndpointError.FromCode("JOB_CREATE_FAILED", $"Job with ID {job.Id} already exists"));
			}

			// Create job in Redis as a hash
			var hashEntries = _jobHashConverter.ConvertToHashEntries(job);
			await _database.HashSetAsync(jobKey, hashEntries);

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

	/// <summary>
	/// Retrieves a job by its unique identifier.
	/// </summary>
	/// <param name="id">The unique identifier of the job to retrieve.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A <see cref="MethodResult{T}"/> containing the job if found, or an error if not found.</returns>
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

	/// <summary>
	/// Updates the complete job entity.
	/// </summary>
	/// <param name="job">The updated job entity.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A <see cref="MethodResult"/> indicating the result of the operation.</returns>
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
			var hashEntries = _jobHashConverter.ConvertToHashEntries(job);
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

	/// <summary>
	/// Atomically claims the next available job for a specific worker.
	/// </summary>
	/// <param name="workerId">The unique identifier of the worker claiming the job.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A <see cref="MethodResult{T}"/> containing the claimed job or null if no jobs available.</returns>
	public async Task<MethodResult<Job>> ClaimNextJobForWorker(Guid workerId, CancellationToken cancellationToken)
	{
		try
		{
			if (cancellationToken.IsCancellationRequested)
			{
				_logger.LogDebug("Claim next job for worker operation cancelled");
				return await Task.FromCanceled<MethodResult<Job>>(cancellationToken);
			}

			// Get the next available job from the queue (oldest first), considering retry delays
			var availableJobIds = await _database.SortedSetRangeByScoreAsync(
				_queueKey,
				start: double.NegativeInfinity,
				stop: GetScoreForTime(_dateTimeProvider.UtcNow),
				exclude: Exclude.None,
				skip: 0,
				take: 1  // Only take the next available job
			);

			if (availableJobIds.Length == 0)
			{
				_logger.LogDebug("No available jobs to claim for worker {WorkerId}", workerId);
				return MethodResult<Job>.Success(default);
			}

			var jobIdString = availableJobIds[0];
			if (!Guid.TryParse(jobIdString, out var jobId))
			{
				_logger.LogDebug("Failed to parse jobId from jobIdString {JobIdString} for worker {WorkerId}", jobIdString, workerId);
				return MethodResult<Job>.Success(default);
			}

			var result = await ClaimSingleJob(jobId, workerId, cancellationToken);
			if (!result.IsSuccess)
			{
				_logger.LogDebug("Failed to claim job for worker {WorkerId}", workerId);
				return MethodResult<Job>.Success(default);
			}

			_logger.LogInformation("Claimed job {JobId} for worker {WorkerId}", jobId, workerId);
			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error claiming next job for worker {WorkerId}", workerId);
			return MethodResult<Job>.Failure(
				AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error claiming job: {ex.Message}", ex));
		}
	}

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
			values:
			[
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
			]
		);

		// Handle the script result
		if (result.IsNull || result.ToString().StartsWith("NOSCRIPT"))
		{
			// Lua script error occurred
			return MethodResult<Job>.Failure(AsyncEndpointError.FromCode("JOB_CLAIM_ERROR", "Could not claim job due to script error"));
		}

		try
		{
			// Check if the script returned an error (Redis error reply)
			if (result.Resp3Type == ResultType.Error)
			{
				var error = result.ToString();
				if (error.Contains("ALREADY_ASSIGNED") || error.Contains("WRONG_STATUS") || error.Contains("RETRY_DELAY"))
				{
					return MethodResult<Job>.Failure(AsyncEndpointError.FromCode("JOB_NOT_CLAIMED", "Could not claim job"));
				}
				return MethodResult<Job>.Failure(AsyncEndpointError.FromCode("JOB_CLAIM_ERROR", $"Redis Lua script error: {error}"));
			}

			// Construct the job from the returned values
			var resultArray = (RedisValue[])result!;

			var claimedJob = new Job
			{
				Id = Guid.Parse(resultArray[0].ToString()),
				Name = resultArray[1].ToString(),
				Status = (JobStatus)int.Parse(resultArray[2].ToString()),
				Headers = string.IsNullOrEmpty(resultArray[3].ToString()) ? [] : Deserialize<Dictionary<string, List<string?>>>(resultArray[3].ToString()) ?? [],
				RouteParams = string.IsNullOrEmpty(resultArray[4].ToString()) ? [] : Deserialize<Dictionary<string, object?>>(resultArray[4].ToString()) ?? [],
				QueryParams = string.IsNullOrEmpty(resultArray[5].ToString()) ? [] : Deserialize<List<KeyValuePair<string, List<string?>>>>(resultArray[5].ToString()) ?? [],
				Payload = resultArray[6].ToString(),
				Result = string.IsNullOrEmpty(resultArray[7].ToString()) ? null : resultArray[7].ToString(),
				Error = string.IsNullOrEmpty(resultArray[8].ToString()) ? null :
						Deserialize<AsyncEndpointError>(resultArray[8].ToString()),
				RetryCount = int.Parse(resultArray[9].ToString()),
				MaxRetries = int.Parse(resultArray[10].ToString()),
				RetryDelayUntil = string.IsNullOrEmpty(resultArray[11].ToString()) ? null :
								 DateTime.Parse(resultArray[11].ToString()),
				WorkerId = workerId, // Newly assigned
				CreatedAt = DateTimeOffset.Parse(resultArray[13].ToString()),
				StartedAt = DateTimeOffset.Parse(resultArray[14].ToString()), // Newly set
				CompletedAt = string.IsNullOrEmpty(resultArray[15].ToString()) ? null : DateTimeOffset.Parse(resultArray[15].ToString()),
				LastUpdatedAt = DateTimeOffset.Parse(resultArray[16].ToString()) // Newly set
			};

			return MethodResult<Job>.Success(claimedJob);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error constructing job object after claiming job {JobId}", jobId);
			return MethodResult<Job>.Failure(AsyncEndpointError.FromCode("JOB_CONSTRUCTION_ERROR", $"Error constructing job object: {ex.Message}"));
		}
	}

	private IDatabase InitializeDatabase(string connectionString)
	{
		try
		{
			var redis = ConnectionMultiplexer.Connect(connectionString);

			// Register for connection events to handle reconnection
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
		// Use timestamp as score for the sorted set to prioritize older jobs
		// If there's a retry delay, use that time instead
		var effectiveTime = job.RetryDelayUntil ?? job.CreatedAt.UtcDateTime;
		return GetScoreForTime(effectiveTime);
	}

	private static double GetScoreForTime(DateTime dateTime)
	{
		return (dateTime - _unixEpoch).TotalSeconds;
	}

	private T? Deserialize<T>(string value)
	{
		if (string.IsNullOrEmpty(value)) return default;
		return _serializer.Deserialize<T>(value);
	}

	public async Task<int> RecoverStuckJobs(long timeoutUnixTime, int maxRetries, double retryDelayBaseSeconds, CancellationToken cancellationToken)
	{
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
                local startedAtUnix = redis.call('HGET', jobKey, 'StartedAtUnix')
                local retryCount = redis.call('HGET', jobKey, 'RetryCount') or '0'
                local maxRetriesForJob = redis.call('HGET', jobKey, 'MaxRetries') or ARGV[2]
                
                -- Check if job is InProgress (status 300) and started more than timeout ago
                if status == '300' and startedAtUnix then -- 300 = JobStatus.InProgress
                    if tonumber(startedAtUnix) < timeoutUnixTime then
                        retryCount = tonumber(retryCount)
                        maxRetriesForJob = tonumber(maxRetriesForJob)
                        
                        if retryCount < maxRetriesForJob then
                            -- Calculate exponential backoff delay
                            local newRetryCount = retryCount + 1
                            local newRetryDelay = math.pow(2, newRetryCount) * retryDelayBaseSeconds
                            local retryUntil = tonumber(currentTime) + newRetryDelay
                            
                            -- Update the job to scheduled status
                            redis.call('HSET', jobKey, 
                                'Status', '200', -- 200 = JobStatus.Scheduled
                                'RetryCount', tostring(newRetryCount),
                                'RetryDelayUntil', tostring(retryUntil),
                                'WorkerId', '', -- Release worker assignment
                                'StartedAt', '', -- Clear started time
                                'StartedAtUnix', '', -- Clear started time
                                'LastUpdatedAt', currentTime)
                            
                            -- Add back to the queue with the retry time as score
                            local jobId = string.gsub(jobKey, 'ae:job:', '')
                            redis.call('ZADD', 'ae:jobs:queue', retryUntil, jobId)
                            
                            recoveredCount = recoveredCount + 1
                        else
                            -- Mark as permanently failed
                            redis.call('HSET', jobKey,
                                'Status', '500', -- 500 = JobStatus.Failed
                                'Error', 'Job failed after maximum retries',
                                'WorkerId', '',
                                'StartedAt', '',
                                'StartedAtUnix', '',
                                'LastUpdatedAt', currentTime)
                        end
                    end
                end
            until cursor == 0
            
            return recoveredCount
    ";

		var result = await _database.ScriptEvaluateAsync(luaScript,
			values: new RedisValue[]
			{
				timeoutUnixTime.ToString(),
				maxRetries.ToString(),
				retryDelayBaseSeconds.ToString(),
				((DateTimeOffset)_dateTimeProvider.UtcNow).ToUnixTimeSeconds().ToString()
			});

		return (int)(long)result;
	}
}
