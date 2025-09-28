using AsyncEndpoints.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AsyncEndpoints.Redis;

/// <summary>
/// Extension methods for configuring and registering AsyncEndpoints Redis services with the dependency injection container.
/// </summary>
public static class RedisServiceCollectionExtensions
{
    /// <summary>
    /// Adds a Redis-based job store implementation to the dependency injection container.
    /// Use this for production deployments that require persistence and distributed processing.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="connectionString">The Redis connection string.</param>
    /// <returns>The <see cref="IServiceCollection"/> for method chaining.</returns>
    public static IServiceCollection AddAsyncEndpointsRedisStore(this IServiceCollection services, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Redis connection string cannot be null or empty.", nameof(connectionString));

        services.AddSingleton<IJobStore>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<RedisJobStore>>();
            var dateTimeProvider = provider.GetRequiredService<IDateTimeProvider>();
            var serializer = provider.GetRequiredService<ISerializer>();
            return new RedisJobStore(logger, connectionString, dateTimeProvider, serializer);
        });

        return services;
    }

    /// <summary>
    /// Adds a Redis-based job store implementation to the dependency injection container.
    /// Use this for production deployments that require persistence and distributed processing.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="connectionMultiplexer">The Redis connection multiplexer instance.</param>
    /// <returns>The <see cref="IServiceCollection"/> for method chaining.</returns>
    public static IServiceCollection AddAsyncEndpointsRedisStore(this IServiceCollection services, IConnectionMultiplexer connectionMultiplexer)
    {
        if (connectionMultiplexer == null)
            throw new ArgumentNullException(nameof(connectionMultiplexer));

        services.AddSingleton<IJobStore>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<RedisJobStore>>();
            var dateTimeProvider = provider.GetRequiredService<IDateTimeProvider>();
            var serializer = provider.GetRequiredService<ISerializer>();
            var database = connectionMultiplexer.GetDatabase();
            return new RedisJobStore(logger, database, dateTimeProvider, serializer);
        });

        return services;
    }

    /// <summary>
    /// Adds a Redis-based job store implementation to the dependency injection container.
    /// Use this for production deployments that require persistence and distributed processing.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="setupAction">Action to configure the Redis connection multiplexer.</param>
    /// <returns>The <see cref="IServiceCollection"/> for method chaining.</returns>
    public static IServiceCollection AddAsyncEndpointsRedisStore(this IServiceCollection services, Action<RedisConfiguration> setupAction)
    {
        var config = new RedisConfiguration();
        setupAction?.Invoke(config);

        if (string.IsNullOrWhiteSpace(config.ConnectionString))
            throw new ArgumentException("Redis connection string cannot be null or empty.");

        services.AddSingleton<IJobStore>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<RedisJobStore>>();
            var dateTimeProvider = provider.GetRequiredService<IDateTimeProvider>();
            var serializer = provider.GetRequiredService<ISerializer>();
            return new RedisJobStore(logger, config.ConnectionString, dateTimeProvider, serializer);
        });

        return services;
    }
}
