namespace AsyncEndpoints.Abstractions.Jobs;

/// <summary>
/// Defines the contract for handling a specific job type.
/// </summary>
/// <typeparam name="TJob">The type of job payload.</typeparam>
public interface IJobHandler<in TJob>
{
	/// <summary>
	/// Processes the job.
	/// </summary>
	Task HandleAsync(TJob job, CancellationToken ct);
}
