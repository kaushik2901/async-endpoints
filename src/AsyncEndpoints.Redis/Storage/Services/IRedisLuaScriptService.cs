using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.Utilities;
using StackExchange.Redis;

namespace AsyncEndpoints.Redis.Storage.Services;

public interface IRedisLuaScriptService
{
	Task<MethodResult<RedisValue[]>> ClaimSingleJob(
		IDatabase database,
		Guid jobId,
		Guid workerId,
		IDateTimeProvider dateTimeProvider,
		CancellationToken cancellationToken = default);

	Task<int> RecoverStuckJobs(
		IDatabase database,
		long timeoutUnixTime,
		int maxRetries,
		double retryDelayBaseSeconds,
		IDateTimeProvider dateTimeProvider,
		CancellationToken cancellationToken = default);
}
