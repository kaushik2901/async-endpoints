namespace AsyncEndpoints.Redis;

/// <summary>
/// Configuration class for Redis settings.
/// </summary>
public class RedisConfiguration
{
    /// <summary>
    /// Gets or sets the Redis connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
}