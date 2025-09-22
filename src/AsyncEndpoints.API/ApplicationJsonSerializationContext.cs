using System.Text.Json.Serialization;
using AsyncEndpoints.API.Models;

namespace AsyncEndpoints.API;

[JsonSerializable(typeof(SampleRequest))]
[JsonSerializable(typeof(SampleResponse))]
public partial class ApplicationJsonSerializationContext : JsonSerializerContext
{

}
