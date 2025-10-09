using AsyncEndpoints.Utilities;
using StackExchange.Redis;

namespace AsyncEndpoints.Redis.Services;

public interface IRedisLuaScriptService
{
	Task<MethodResult<RedisValue[]>> ClaimSingleJob(IDatabase database, Guid jobId, Guid workerId);

	Task<int> RecoverStuckJobs(IDatabase database, long timeoutUnixTime, int maxRetries, double retryDelayBaseSeconds);
}
