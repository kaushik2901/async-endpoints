using System.Text.Json.Serialization;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace AsyncEndpoints;

[JsonSerializable(typeof(Job))]
[JsonSerializable(typeof(JobResponse))]
[JsonSerializable(typeof(ProblemDetails))]
public partial class AsyncEndpointsJsonSerializationContext : JsonSerializerContext
{
}
