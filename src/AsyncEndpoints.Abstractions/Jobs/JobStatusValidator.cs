namespace AsyncEndpoints.Abstractions.Jobs;

public static class JobStatusValidator
{
	public static bool IsValidTransition(JobStatus from, JobStatus to)
	{
		if (from == to) return true;
		return (from, to) switch
		{
			(JobStatus.Queued, JobStatus.Processing) => true,
			(JobStatus.Processing, JobStatus.Completed) => true,
			(JobStatus.Processing, JobStatus.Failed) => true,
			(JobStatus.Processing, JobStatus.Queued) => true,
			(JobStatus.Failed, JobStatus.Queued) => true,
			(JobStatus.Failed, JobStatus.DeadLettered) => true,
			_ => false
		};
	}
}
