using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AsyncEndpoints.Redis.Services;

/// <summary>
/// Provides functionality to execute Redis Lua scripts for job operations.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="RedisLuaScriptService"/> class.
/// </remarks>
/// <param name="logger">The logger instance.</param>
public class RedisLuaScriptService(ILogger<RedisLuaScriptService> logger, IDateTimeProvider dateTimeProvider) : IRedisLuaScriptService
{
	private readonly ILogger<RedisLuaScriptService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

	/// <summary>
	/// Atomically claims a single job for a specific worker using a Redis Lua script.
	/// </summary>
	/// <param name="database">The Redis database instance.</param>
	/// <param name="jobId">The unique identifier of the job to claim.</param>
	/// <param name="workerId">The unique identifier of the worker claiming the job.</param>
	/// <returns>A <see cref="MethodResult{RedisValue[]}"/> containing the raw Redis values of the claimed job or an error if the operation failed.</returns>
	public async Task<MethodResult<RedisValue[]>> ClaimSingleJob(IDatabase database, Guid jobId, Guid workerId)
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

			-- Convert startedAt to Unix timestamp for easier comparison in recovery
			local startedAtUnix = tonumber(currentTime) -- Use the current time provided as Unix timestamp

			-- Claim the job atomically
			redis.call('HSET', jobKey, 'Status', newStatus)
			redis.call('HSET', jobKey, 'WorkerId', newWorkerId)
			redis.call('HSET', jobKey, 'StartedAt', newStartedAt)
			redis.call('HSET', jobKey, 'StartedAtUnix', startedAtUnix)
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

		var result = await database.ScriptEvaluateAsync(
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
                "ae:jobs:queue",                         // Queue key
                jobId.ToString(),
				currentTime                              // Current time for retry delay check
            ]
		);

		// Handle the script result
		if (result.IsNull || result.ToString().StartsWith("NOSCRIPT"))
		{
			// Lua script error occurred
			return MethodResult<RedisValue[]>.Failure(AsyncEndpointError.FromCode("JOB_CLAIM_ERROR", "Could not claim job due to script error"));
		}

		try
		{
			// Check if the script returned an error (Redis error reply)
			if (result.Resp3Type == ResultType.Error)
			{
				var error = result.ToString();
				if (error.Contains("ALREADY_ASSIGNED") || error.Contains("WRONG_STATUS") || error.Contains("RETRY_DELAY"))
				{
					return MethodResult<RedisValue[]>.Failure(AsyncEndpointError.FromCode("JOB_NOT_CLAIMED", "Could not claim job"));
				}
				return MethodResult<RedisValue[]>.Failure(AsyncEndpointError.FromCode("JOB_CLAIM_ERROR", $"Redis Lua script error: {error}"));
			}

			// Return the Redis values array
			var resultArray = (RedisValue[])result!;
			return MethodResult<RedisValue[]>.Success(resultArray);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing result after claiming job {JobId}", jobId);
			return MethodResult<RedisValue[]>.Failure(AsyncEndpointError.FromCode("JOB_PROCESSING_ERROR", $"Error processing job claim result: {ex.Message}"));
		}
	}

	/// <summary>
	/// Recovers stuck jobs that have been in progress longer than the specified timeout using a Redis Lua script.
	/// </summary>
	/// <param name="database">The Redis database instance.</param>
	/// <param name="timeoutUnixTime">The Unix timestamp after which jobs are considered stuck.</param>
	/// <param name="maxRetries">The maximum number of retries allowed for a job.</param>
	/// <param name="retryDelayBaseSeconds">The base delay in seconds for exponential backoff retry strategy.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>The number of jobs that were successfully recovered.</returns>
	public async Task<int> RecoverStuckJobs(IDatabase database, long timeoutUnixTime, int maxRetries, double retryDelayBaseSeconds)
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
					if status == '300' and startedAtUnix and startedAtUnix ~= '' then -- 300 = JobStatus.InProgress
						if tonumber(startedAtUnix) < timeoutUnixTime then
							retryCount = tonumber(retryCount)
							maxRetriesForJob = tonumber(maxRetriesForJob)
                        
							if retryCount < maxRetriesForJob then
								-- Calculate exponential backoff delay
								local newRetryCount = retryCount + 1
								local newRetryDelay = math.pow(2, newRetryCount) * retryDelayBaseSeconds
								local retryUntil = tonumber(currentTime) + newRetryDelay
                            
								-- We need to make sure RetryDelayUntil gets stored in ISO format
								-- Extract date components and convert to ISO format
								local retryUntilYear = math.floor(retryUntil / (365.25 * 24 * 3600)) + 1970
								-- This approach is complex; the proper way would be to ensure the retryUntil value 
								-- represents time in ISO format already, but for now we'll keep the old implementation
                            
								-- Update the job to scheduled status, storing retry delay as Unix timestamp
								redis.call('HSET', jobKey, 
									'Status', '200', -- 200 = JobStatus.Scheduled
									'RetryCount', tostring(newRetryCount),
									'RetryDelayUntil', tostring(retryUntil), -- Store as Unix timestamp string for now
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
				end
			until cursor == 0
        
			return recoveredCount
		";

		var currentTimeUnix = _dateTimeProvider.DateTimeOffsetNow.ToUnixTimeSeconds();

		var result = await database.ScriptEvaluateAsync(luaScript,
			values:
			[
				timeoutUnixTime.ToString(),
				maxRetries.ToString(),
				retryDelayBaseSeconds.ToString(),
				currentTimeUnix.ToString()
			]);

		return (int)(long)result;
	}

	/// <summary>
	/// Generates the Redis key for a job based on its ID.
	/// </summary>
	/// <param name="jobId">The unique identifier of the job.</param>
	/// <returns>The Redis key string for the job.</returns>
	private static string GetJobKey(Guid jobId) => $"ae:job:{jobId}";
}
