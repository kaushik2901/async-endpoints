using System.Text.Json.Serialization;

namespace RedisExampleCore;

[JsonSerializable(typeof(ExampleRequest))]
[JsonSerializable(typeof(ExampleResponse))]
public partial class ApplicationJsonSerializationContext : JsonSerializerContext
{
}
