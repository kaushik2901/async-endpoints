namespace AsyncEndpoints.Abstractions.Jobs;

/// <summary>
/// Exception thrown when an invalid job status transition is attempted.
/// </summary>
public class InvalidJobStatusTransitionException : Exception
{
	public JobStatus From { get; }
	public JobStatus To { get; }

	public InvalidJobStatusTransitionException(JobStatus from, JobStatus to)
		: base($"Invalid job status transition from {from} to {to}.")
	{
		From = from;
		To = to;
	}
}
