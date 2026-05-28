using AsyncEndpoints.Abstractions.Common;
using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Core.Legacy.Handlers;
using AsyncEndpoints.Core.Legacy.JobProcessing;
using System.Text.Json.Serialization;

namespace AsyncEndpoints;

#pragma warning disable CS0618 // Type or member is obsolete - intentionally supporting legacy types for backward compat
[JsonSerializable(typeof(Job), TypeInfoPropertyName = "Job")]
[JsonSerializable(typeof(NoBodyRequest))]
[JsonSerializable(typeof(AsyncEndpointError))]
[JsonSerializable(typeof(ExceptionInfo))]
[JsonSerializable(typeof(InnerExceptionInfo))]
[JsonSerializable(typeof(JobRecord), TypeInfoPropertyName = "JobRecord")]
[JsonSerializable(typeof(JobDescriptor), TypeInfoPropertyName = "JobDescriptor")]
[JsonSerializable(typeof(Core.Legacy.JobProcessing.JobStatus), TypeInfoPropertyName = "CoreJobStatus")]
[JsonSerializable(typeof(Abstractions.Jobs.JobStatus), TypeInfoPropertyName = "AbstractionsJobStatus")]
#pragma warning restore CS0618
public partial class AsyncEndpointsJsonSerializationContext : JsonSerializerContext
{
}
