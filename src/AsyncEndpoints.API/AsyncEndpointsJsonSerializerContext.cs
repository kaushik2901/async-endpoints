using System.Text.Json.Serialization;
using AsyncEndpoints.API.Models;

[JsonSerializable(typeof(SampleRequest))]
[JsonSerializable(typeof(SampleResponse))]
internal partial class AsyncEndpointsJsonSerializerContext : JsonSerializerContext
{

}
