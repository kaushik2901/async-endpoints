using AsyncEndpoints.AspNetCore.Models;
using System.Text.Json.Serialization;

namespace AsyncEndpoints.AspNetCore.Infrastructure;

[JsonSerializable(typeof(HttpJobPayload))]
[JsonSerializable(typeof(JobStatusResponse))]
[JsonSerializable(typeof(JobResultResponse))]
[JsonSerializable(typeof(JobSubmittedResponse))]
public partial class AspNetCoreJsonContext : JsonSerializerContext
{
}
