using AsyncEndpoints.Abstractions.Infrastructure;
using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.Redis.Configuration;
using AsyncEndpoints.Redis.Services;
using AsyncEndpoints.Redis.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AsyncEndpoints.Redis.Extensions;

public static class RedisServiceCollectionExtensions
{
	public static IServiceCollection AddAsyncEndpointsRedisStore(this IServiceCollection services, string connectionString)
	{
		if (string.IsNullOrWhiteSpace(connectionString))
			throw new ArgumentException("Redis connection string cannot be null or empty.", nameof(connectionString));

		services.AddSingleton<IJobHashConverter, JobHashConverter>();
		services.AddSingleton<IRedisLuaScriptService, RedisLuaScriptService>();

		services.AddSingleton<IJobStore>(provider =>
		{
			var logger = provider.GetRequiredService<ILogger<RedisJobStore>>();
			var dateTimeProvider = provider.GetRequiredService<IDateTimeProvider>();
			var jobHashConverter = provider.GetRequiredService<IJobHashConverter>();
			var luaScriptService = provider.GetRequiredService<IRedisLuaScriptService>();
			var database = InitializeDatabase(connectionString, logger);
			return new RedisJobStore(logger, database, dateTimeProvider, jobHashConverter, luaScriptService);
		});

		return services;
	}

	public static IServiceCollection AddAsyncEndpointsRedisStore(this IServiceCollection services, IConnectionMultiplexer connectionMultiplexer)
	{
		ArgumentNullException.ThrowIfNull(connectionMultiplexer);

		services.AddSingleton<IJobHashConverter, JobHashConverter>();
		services.AddSingleton<IRedisLuaScriptService, RedisLuaScriptService>();

		services.AddSingleton<IJobStore>(provider =>
		{
			var logger = provider.GetRequiredService<ILogger<RedisJobStore>>();
			var dateTimeProvider = provider.GetRequiredService<IDateTimeProvider>();
			var jobHashConverter = provider.GetRequiredService<IJobHashConverter>();
			var luaScriptService = provider.GetRequiredService<IRedisLuaScriptService>();
			var database = connectionMultiplexer.GetDatabase();
			return new RedisJobStore(logger, database, dateTimeProvider, jobHashConverter, luaScriptService);
		});

		return services;
	}

	public static IServiceCollection AddAsyncEndpointsRedisStore(this IServiceCollection services, Action<RedisConfiguration> setupAction)
	{
		var config = new RedisConfiguration();
		setupAction?.Invoke(config);

		if (string.IsNullOrWhiteSpace(config.ConnectionString))
			throw new ArgumentException("Redis connection string cannot be null or empty.");

		services.AddSingleton<IJobHashConverter, JobHashConverter>();
		services.AddSingleton<IRedisLuaScriptService, RedisLuaScriptService>();

		services.AddSingleton<IJobStore>(provider =>
		{
			var logger = provider.GetRequiredService<ILogger<RedisJobStore>>();
			var dateTimeProvider = provider.GetRequiredService<IDateTimeProvider>();
			var jobHashConverter = provider.GetRequiredService<IJobHashConverter>();
			var luaScriptService = provider.GetRequiredService<IRedisLuaScriptService>();
			var database = InitializeDatabase(config.ConnectionString, logger);
			return new RedisJobStore(logger, database, dateTimeProvider, jobHashConverter, luaScriptService);
		});

		return services;
	}

	private static IDatabase InitializeDatabase(string connectionString, ILogger logger)
	{
		var redis = ConnectionMultiplexer.Connect(connectionString);

		redis.ConnectionFailed += (sender, e) =>
			logger.LogError(e.Exception, "Redis connection failed: {ErrorMessage}", e.Exception?.Message);
		redis.ConnectionRestored += (sender, e) =>
			logger.LogInformation("Redis connection restored");

		return redis.GetDatabase();
	}
}
