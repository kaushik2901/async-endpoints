namespace AsyncEndpoints.Background;

/// <summary>
/// Represents the state in job claiming and enqueuing
/// </summary>
public enum JobClaimingState
{
	/// <summary>
	/// State when a job was successfully claimed and enqueued for processing
	/// </summary>
	JobSuccessfullyEnqueued = 100,

	/// <summary>
	/// State when no job was found during polling
	/// </summary>
	NoJobFound = 200,

	/// <summary>
	/// State when a job was claimed but failed to enqueue
	/// </summary>
	FailedToEnqueue = 300,

	/// <summary>
	/// State when an error occurred during job processing
	/// </summary>
	ErrorOccurred = 400
}
