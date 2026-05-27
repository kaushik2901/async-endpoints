using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Handlers;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.Utilities;
using System.Text.Json.Serialization;

namespace AsyncEndpoints;

[JsonSerializable(typeof(Job), TypeInfoPropertyName = "Job")]
[JsonSerializable(typeof(NoBodyRequest))]
[JsonSerializable(typeof(AsyncEndpointError))]
[JsonSerializable(typeof(ExceptionInfo))]
[JsonSerializable(typeof(InnerExceptionInfo))]
[JsonSerializable(typeof(JobRecord), TypeInfoPropertyName = "JobRecord")]
[JsonSerializable(typeof(JobDescriptor), TypeInfoPropertyName = "JobDescriptor")]
[JsonSerializable(typeof(AsyncEndpoints.JobProcessing.JobStatus), TypeInfoPropertyName = "CoreJobStatus")]
[JsonSerializable(typeof(AsyncEndpoints.Abstractions.Jobs.JobStatus), TypeInfoPropertyName = "AbstractionsJobStatus")]
public partial class AsyncEndpointsJsonSerializationContext : JsonSerializerContext
{
}
