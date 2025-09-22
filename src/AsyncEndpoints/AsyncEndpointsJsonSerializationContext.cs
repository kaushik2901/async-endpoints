using System.Text.Json.Serialization;
using AsyncEndpoints.Entities;

namespace AsyncEndpoints;

[JsonSerializable(typeof(Job))]
public partial class AsyncEndpointsJsonSerializationContext : JsonSerializerContext
{
}
