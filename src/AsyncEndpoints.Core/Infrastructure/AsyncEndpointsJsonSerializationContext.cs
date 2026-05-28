using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Utilities;
using AsyncEndpoints.Core.Handlers;
using AsyncEndpoints.Core.JobProcessing;
using System.Text.Json.Serialization;

namespace AsyncEndpoints;

[JsonSerializable(typeof(Job), TypeInfoPropertyName = "Job")]
[JsonSerializable(typeof(NoBodyRequest))]
[JsonSerializable(typeof(AsyncEndpointError))]
[JsonSerializable(typeof(ExceptionInfo))]
[JsonSerializable(typeof(InnerExceptionInfo))]
[JsonSerializable(typeof(JobRecord), TypeInfoPropertyName = "JobRecord")]
[JsonSerializable(typeof(JobDescriptor), TypeInfoPropertyName = "JobDescriptor")]
[JsonSerializable(typeof(Core.JobProcessing.JobStatus), TypeInfoPropertyName = "CoreJobStatus")]
[JsonSerializable(typeof(Abstractions.Jobs.JobStatus), TypeInfoPropertyName = "AbstractionsJobStatus")]
public partial class AsyncEndpointsJsonSerializationContext : JsonSerializerContext
{
}
