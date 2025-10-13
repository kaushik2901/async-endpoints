using System.Text.Json.Serialization;
using InMemoryExampleAPI.Models;

namespace AsyncEndpoints.API;

[JsonSerializable(typeof(ExampleRequest))]
[JsonSerializable(typeof(ExampleResponse))]
public partial class ApplicationJsonSerializationContext : JsonSerializerContext
{

}
