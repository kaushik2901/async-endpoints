using AsyncEndpoints.Abstractions.Jobs;

namespace AsyncEndpoints.Abstractions.Submission;

/// <summary>
/// Entry point for submitting jobs to the system.
/// </summary>
public interface IJobSubmitter
{
	/// <summary>
	/// Submits a job for asynchronous processing.
	/// </summary>
	/// <typeparam name="TJob">The job payload type.</typeparam>
	/// <param name="job">The job payload.</param>
	/// <param name="channel">The channel to submit the job to. If null, use default.</param>
	/// <param name="priority">The priority of the job. If null, use default.</param>
	/// <param name="partitionBy">The key used to partition jobs for ordered processing. If null, no partitioning.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The created job record details.</returns>
	Task<JobRecord> SubmitAsync<TJob>(
		TJob job,
		string? channel = null,
		int? priority = null,
		string? partitionBy = null,
		CancellationToken ct = default) where TJob : notnull;
}
