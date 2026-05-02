namespace AsyncEndpoints.Abstractions.Jobs;

/// <summary>
/// Represents the possible states of a job.
/// </summary>
public enum JobStatus
{
	/// <summary>
	/// Job is waiting to be processed.
	/// </summary>
	Queued = 0,

	/// <summary>
	/// Job is currently being processed by a worker.
	/// </summary>
	Processing = 1,

	/// <summary>
	/// Job has completed successfully.
	/// </summary>
	Completed = 2,

	/// <summary>
	/// Job processing failed, but it may be retried.
	/// </summary>
	Failed = 3,

	/// <summary>
	/// Job failed and exceeded maximum retries.
	/// </summary>
	DeadLettered = 4,

	/// <summary>
	/// Job was cancelled.
	/// </summary>
	Cancelled = 5
}
