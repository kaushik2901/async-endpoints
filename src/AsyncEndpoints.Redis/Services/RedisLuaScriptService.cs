using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AsyncEndpoints.Redis.Services;

/// <inheritdoc />
public class RedisLuaScriptService(ILogger<RedisLuaScriptService> logger, IDateTimeProvider dateTimeProvider) : IRedisLuaScriptService
{
	private readonly ILogger<RedisLuaScriptService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

	/// <inheritdoc />
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
			
			-- Add to in-progress set with started timestamp as score for efficient recovery scanning
			redis.call('ZADD', 'ae:jobs:inprogress', startedAtUnix, jobId)

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

	/// <inheritdoc />
	public async Task<int> RecoverStuckJobs(IDatabase database, long timeoutUnixTime, int maxRetries)
	{
		var luaScript = @"
			local timeoutUnixTime = tonumber(ARGV[1])
			local maxRetries = tonumber(ARGV[2])
			local currentTimeUnix = tonumber(ARGV[3])
			local currentTimeIso = ARGV[4]
			local inProgressStatus = tonumber(ARGV[5])
			local scheduledStatus = tonumber(ARGV[6])
			local failedStatus = tonumber(ARGV[7])

			-- Get all in-progress jobs that started before the timeout
			local inProgressJobIds = redis.call('ZRANGEBYSCORE', 'ae:jobs:inprogress', '-inf', timeoutUnixTime - 1)

			local recoveredCount = 0

			for _, jobId in ipairs(inProgressJobIds) do
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

						redis.call('ZADD', 'ae:jobs:queue', currentTimeUnix, jobId)
						redis.call('ZREM', 'ae:jobs:inprogress', jobId)
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

						redis.call('ZREM', 'ae:jobs:inprogress', jobId)
					end
				end
			end

			return recoveredCount
		";

		var now = _dateTimeProvider.DateTimeOffsetNow;
		var currentTimeUnix = now.ToUnixTimeSeconds();
		var currentTimeIso = now.ToString("O"); // ISO 8601 format

		var result = await database.ScriptEvaluateAsync(luaScript,
			values:
			[
				timeoutUnixTime.ToString(),
				maxRetries.ToString(),
				currentTimeUnix.ToString(),
				currentTimeIso,
				((int)JobStatus.InProgress).ToString(),
				((int)JobStatus.Scheduled).ToString(),
				((int)JobStatus.Failed).ToString()
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
