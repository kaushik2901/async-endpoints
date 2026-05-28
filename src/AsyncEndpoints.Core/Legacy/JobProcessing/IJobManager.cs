using AsyncEndpoints.Abstractions.Common;

namespace AsyncEndpoints.Core.Legacy.JobProcessing;

[Obsolete("Use the new pipeline with IJobStore/IJobSubmitter directly.")]
public interface IJobManager
{
	Task<MethodResult<Job>> SubmitJob(
		string jobName,
		string payload,
		Guid jobId,
		Dictionary<string, List<string?>> headers,
		Dictionary<string, object?> routeParams,
		List<KeyValuePair<string, List<string?>>> queryParams,
		CancellationToken cancellationToken);

	/// <summary>
	/// Claims the next available job for processing by a worker
	/// </summary>
	Task<MethodResult<Job>> ClaimNextAvailableJob(Guid workerId, CancellationToken cancellationToken);

	/// <summary>
	/// Processes a successful job completion
	/// </summary>
	Task<MethodResult> ProcessJobSuccess(Guid jobId, string result, CancellationToken cancellationToken);

	/// <summary>
	/// Processes a failed job (with potential retry logic)
	/// </summary>
	Task<MethodResult> ProcessJobFailure(Guid jobId, AsyncEndpointError error, CancellationToken cancellationToken);

	/// <summary>
	/// Gets a job by its ID
	/// </summary>
	Task<MethodResult<Job>> GetJobById(Guid jobId, CancellationToken cancellationToken);
}
