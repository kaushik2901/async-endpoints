using System.Text.Json.Serialization;
using InMemoryExampleAPI.Models;

namespace AsyncEndpoints.API;

[JsonSerializable(typeof(SampleRequest))]
[JsonSerializable(typeof(SampleResponse))]
public partial class ApplicationJsonSerializationContext : JsonSerializerContext
{

}
