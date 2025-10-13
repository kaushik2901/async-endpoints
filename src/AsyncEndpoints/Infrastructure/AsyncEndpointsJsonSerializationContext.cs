using System.Text.Json.Serialization;
using AsyncEndpoints.Handlers;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace AsyncEndpoints;

[JsonSerializable(typeof(Job))]
[JsonSerializable(typeof(JobResponse))]
[JsonSerializable(typeof(NoBodyRequest))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(AsyncEndpointError))]
[JsonSerializable(typeof(ExceptionInfo))]
[JsonSerializable(typeof(InnerExceptionInfo))]
public partial class AsyncEndpointsJsonSerializationContext : JsonSerializerContext
{
}
