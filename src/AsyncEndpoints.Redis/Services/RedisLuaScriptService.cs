using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text;

namespace AsyncEndpoints.Redis.Services;

public class RedisLuaScriptService : IRedisLuaScriptService
{
    private readonly ILogger<RedisLuaScriptService> _logger;

    public RedisLuaScriptService(ILogger<RedisLuaScriptService> logger)
    {
        _logger = logger;
    }

    public async Task<string> EnqueueJobAsync(IDatabase database, string jobId, string channel, double score, HashEntry[] hashEntries)
    {
        var script = @"
            local jobKey = 'ae:job:' .. ARGV[1]
            local queueKey = 'ae:queue:' .. ARGV[2]
            local score = tonumber(ARGV[3])

            for i = 4, #ARGV, 2 do
                redis.call('HSET', jobKey, ARGV[i], ARGV[i + 1])
            end

            redis.call('ZADD', queueKey, score, ARGV[1])
            return ARGV[1]
        ";

        var args = new List<RedisValue> { jobId, channel, score.ToString() };
        foreach (var entry in hashEntries)
        {
            args.Add(entry.Name);
            args.Add(entry.Value);
        }

        var result = await database.ScriptEvaluateAsync(script, values: args.ToArray());
        return result.ToString();
    }

    public async Task<RedisValue[]> DequeueJobAsync(IDatabase database, string channel, string nowIso, string nowUnix, string? partitions)
    {
        var script = @"
            local queueKey = 'ae:queue:' .. KEYS[1]
            local heartbeatKey = 'ae:heartbeat'
            local nowIso = ARGV[1]
            local nowUnix = ARGV[2]
            local partitionFilter = ARGV[3]

            local jobIds = redis.call('ZRANGE', queueKey, 0, -1)

            for _, jobId in ipairs(jobIds) do
                local jobKey = 'ae:job:' .. jobId
                local status = redis.call('HGET', jobKey, 'Status')

                if status and tonumber(status) == 100 then
                    local partitionOk = true
                    if partitionFilter and partitionFilter ~= '' then
                        local jobPartition = redis.call('HGET', jobKey, 'Partition')
                        partitionOk = false
                        if jobPartition and jobPartition ~= '' then
                            for p in string.gmatch(partitionFilter, '([^,]+)') do
                                if p == jobPartition then
                                    partitionOk = true
                                    break
                                end
                            end
                        end
                    end

                    if partitionOk then
                        redis.call('HSET', jobKey, 'Status', '300', 'StartedAt', nowIso, 'LastHeartbeat', nowIso)
                        redis.call('ZREM', queueKey, jobId)
                        redis.call('ZADD', heartbeatKey, nowUnix, jobId)
                        local data = redis.call('HGETALL', jobKey)
                        return data
                    end
                else
                    redis.call('ZREM', queueKey, jobId)
                end
            end

            return nil
        ";

        var result = await database.ScriptEvaluateAsync(script, keys: [new RedisKey(channel)], values: [nowIso, nowUnix, partitions ?? ""]);

        if (result.IsNull)
            return [];

        return (RedisValue[])result!;
    }

    public async Task HeartbeatJobAsync(IDatabase database, string jobId, string nowIso, string nowUnix)
    {
        var script = @"
            local jobKey = 'ae:job:' .. ARGV[1]
            local heartbeatKey = 'ae:heartbeat'

            redis.call('HSET', jobKey, 'LastHeartbeat', ARGV[2])
            redis.call('ZADD', heartbeatKey, tonumber(ARGV[3]), ARGV[1])
        ";

        await database.ScriptEvaluateAsync(script, values: [jobId, nowIso, nowUnix]);
    }

    public async Task<int> ReclaimStaleJobsAsync(IDatabase database, long staleCutoffUnix, string nowIso, string nowUnix)
    {
        var script = @"
            local heartbeatKey = 'ae:heartbeat'
            local cutoff = tonumber(ARGV[1])
            local currentTime = ARGV[3]

            local staleJobIds = redis.call('ZRANGEBYSCORE', heartbeatKey, '-inf', cutoff)
            local reclaimed = 0

            for _, jobId in ipairs(staleJobIds) do
                local jobKey = 'ae:job:' .. jobId
                local channel = redis.call('HGET', jobKey, 'Channel')

                redis.call('HSET', jobKey, 'Status', '100', 'StartedAt', '', 'LastHeartbeat', '')
                redis.call('ZREM', heartbeatKey, jobId)

                if channel and channel ~= '' then
                    redis.call('ZADD', 'ae:queue:' .. channel, tonumber(currentTime), jobId)
                end

                reclaimed = reclaimed + 1
            end

            return reclaimed
        ";

        var result = await database.ScriptEvaluateAsync(script, values: [staleCutoffUnix.ToString(), nowIso, nowUnix]);
        return (int)(long)result;
    }
}
