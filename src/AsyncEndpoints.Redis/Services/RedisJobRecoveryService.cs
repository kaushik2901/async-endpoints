using AsyncEndpoints.JobProcessing;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AsyncEndpoints.Redis.Services;

public class RedisJobRecoveryService : IJobRecoveryService
{
	private readonly ILogger<RedisJobRecoveryService> _logger;
	private readonly IDatabase _database;
	private readonly IRedisLuaScriptService _redisLuaScriptService;

	public RedisJobRecoveryService(ILogger<RedisJobRecoveryService> logger, string connectionString, IRedisLuaScriptService redisLuaScriptService)
	{
		_logger = logger;
		_redisLuaScriptService = redisLuaScriptService;
		_database = InitializeDatabase(connectionString ?? throw new ArgumentNullException(nameof(connectionString)));
	}

	public bool SupportsJobRecovery => true; // Redis supports recovery

	/// <inheritdoc />
	public async Task<int> RecoverStuckJobs(long timeoutUnixTime, int maxRetries, CancellationToken cancellationToken)
	{
		return await _redisLuaScriptService.RecoverStuckJobs(_database, timeoutUnixTime, maxRetries);
	}

	private IDatabase InitializeDatabase(string connectionString)
	{
		var redis = ConnectionMultiplexer.Connect(connectionString);

		// Register for connection events to handle reconnection
		redis.ConnectionFailed += (sender, e) =>
			_logger.LogError(e.Exception, "Redis connection failed: {ErrorMessage}", e.Exception?.Message);
		redis.ConnectionRestored += (sender, e) =>
			_logger.LogInformation("Redis connection restored");

		return redis.GetDatabase();
	}
}
