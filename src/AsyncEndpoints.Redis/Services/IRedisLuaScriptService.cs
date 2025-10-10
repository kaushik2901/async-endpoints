using AsyncEndpoints.Utilities;
using StackExchange.Redis;

namespace AsyncEndpoints.Redis.Services;

/// <summary>
/// Provides functionality for executing Lua scripts against Redis for atomic operations.
/// </summary>
public interface IRedisLuaScriptService
{
	/// <summary>
	/// Claims a single job atomically using a Lua script to ensure thread safety.
	/// </summary>
	/// <param name="database">The Redis database instance.</param>
	/// <param name="jobId">The unique identifier of the job to claim.</param>
	/// <param name="workerId">The unique identifier of the worker claiming the job.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains a MethodResult with Redis values.</returns>
	Task<MethodResult<RedisValue[]>> ClaimSingleJob(IDatabase database, Guid jobId, Guid workerId);

	/// <summary>
	/// Recovers stuck jobs that were in progress beyond the specified timeout by executing a Lua script.
	/// </summary>
	/// <param name="database">The Redis database instance.</param>
	/// <param name="timeoutUnixTime">The Unix timestamp before which jobs should be considered stuck.</param>
	/// <param name="maxRetries">The maximum number of retries for failed jobs.</param>
	/// <returns>The number of jobs recovered.</returns>
	Task<int> RecoverStuckJobs(IDatabase database, long timeoutUnixTime, int maxRetries);
}
