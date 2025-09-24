using System.Text.Json.Serialization;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Utilities;

namespace AsyncEndpoints;

[JsonSerializable(typeof(Job))]
[JsonSerializable(typeof(JobResponse))]
public partial class AsyncEndpointsJsonSerializationContext : JsonSerializerContext
{
}
