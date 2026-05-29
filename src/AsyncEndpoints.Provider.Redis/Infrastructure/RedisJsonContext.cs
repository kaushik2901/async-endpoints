using System.Text.Json.Serialization;

namespace AsyncEndpoints.Provider.Redis.Infrastructure;

[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class RedisJsonContext : JsonSerializerContext
{
}
