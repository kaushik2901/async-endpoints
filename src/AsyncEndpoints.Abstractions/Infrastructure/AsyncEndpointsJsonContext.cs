using AsyncEndpoints.Abstractions.Common;
using AsyncEndpoints.Abstractions.Jobs;
using System.Text.Json.Serialization;

namespace AsyncEndpoints.Abstractions.Infrastructure;

[JsonSerializable(typeof(AsyncEndpointError))]
[JsonSerializable(typeof(ExceptionInfo))]
[JsonSerializable(typeof(InnerExceptionInfo))]
[JsonSerializable(typeof(JobRecord))]
[JsonSerializable(typeof(JobDescriptor))]
[JsonSerializable(typeof(JobStatus))]
public partial class AsyncEndpointsJsonContext : JsonSerializerContext
{
}
