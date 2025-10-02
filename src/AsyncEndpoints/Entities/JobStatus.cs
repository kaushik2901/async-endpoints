namespace AsyncEndpoints.Entities;

/// <summary>
/// Represents the status of an asynchronous job in the system.
/// </summary>
public enum JobStatus
{
	/// <summary>
	/// The job has been created and is waiting to be processed.
	/// </summary>
	Queued = 100,

	/// <summary>
	/// The job has been scheduled for delayed execution.
	/// </summary>
	Scheduled = 200,

	/// <summary>
	/// The job is currently being processed by a worker.
	/// </summary>
	InProgress = 300,

	/// <summary>
	/// The job has completed successfully.
	/// </summary>
	Completed = 400,

	/// <summary>
	/// The job has failed and will not be retried.
	/// </summary>
	Failed = 500,

	/// <summary>
	/// The job has been canceled and will not be processed.
	/// </summary>
	Canceled = 600,
}
