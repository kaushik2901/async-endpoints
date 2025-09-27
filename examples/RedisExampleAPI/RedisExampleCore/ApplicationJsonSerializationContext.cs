using System.Text.Json.Serialization;

namespace RedisExampleCore;

[JsonSerializable(typeof(ExampleJobRequest))]
[JsonSerializable(typeof(ExampleJobResponse))]
public partial class ApplicationJsonSerializationContext : JsonSerializerContext
{
}
