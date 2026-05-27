using StackExchange.Redis;

namespace AsyncEndpoints.Redis.Services;

public interface IRedisLuaScriptService
{
    Task<string> EnqueueJobAsync(IDatabase database, string jobId, string channel, double score, HashEntry[] hashEntries);
    Task<RedisValue[]> DequeueJobAsync(IDatabase database, string channel, string nowIso, string nowUnix, string? partitions);
    Task HeartbeatJobAsync(IDatabase database, string jobId, string nowIso, string nowUnix);
    Task<int> ReclaimStaleJobsAsync(IDatabase database, long staleCutoffUnix, string nowIso, string nowUnix);
}
