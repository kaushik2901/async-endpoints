using AsyncEndpoints.AspNetCore.Models;
using System.Text.Json.Serialization;

namespace AsyncEndpoints.AspNetCore;

[JsonSerializable(typeof(HttpJobPayload))]
[JsonSerializable(typeof(JobStatusResponse))]
[JsonSerializable(typeof(JobResultResponse))]
[JsonSerializable(typeof(JobSubmittedResponse))]
public partial class AsyncEndpointsAspNetCoreJsonSerializationContext : JsonSerializerContext
{
}
