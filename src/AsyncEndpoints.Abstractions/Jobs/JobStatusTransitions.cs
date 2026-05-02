namespace AsyncEndpoints.Abstractions.Jobs;

/// <summary>
/// Logic for validating job status transitions.
/// </summary>
public static class JobStatusTransitions
{
	private static readonly Dictionary<JobStatus, HashSet<JobStatus>> AllowedTransitions = new()
	{
		[JobStatus.Queued] = [JobStatus.Processing, JobStatus.Cancelled],
		[JobStatus.Processing] = [JobStatus.Completed, JobStatus.Failed, JobStatus.Cancelled],
		[JobStatus.Failed] = [JobStatus.Queued, JobStatus.DeadLettered, JobStatus.Cancelled],
		[JobStatus.Completed] = [],
		[JobStatus.DeadLettered] = [],
		[JobStatus.Cancelled] = []
	};

	/// <summary>
	/// Asserts that a transition from one status to another is valid.
	/// </summary>
	/// <param name="from">The current status.</param>
	/// <param name="to">The target status.</param>
	/// <exception cref="InvalidJobStatusTransitionException">Thrown if the transition is invalid.</exception>
	public static void AssertTransitionAllowed(JobStatus from, JobStatus to)
	{
		if (from == to) return;

		if (!AllowedTransitions.TryGetValue(from, out var targets) || !targets.Contains(to))
		{
			throw new InvalidJobStatusTransitionException(from, to);
		}
	}

	/// <summary>
	/// Checks if a transition from one status to another is valid.
	/// </summary>
	public static bool IsTransitionAllowed(JobStatus from, JobStatus to)
	{
		if (from == to) return true;
		return AllowedTransitions.TryGetValue(from, out var targets) && targets.Contains(to);
	}
}
