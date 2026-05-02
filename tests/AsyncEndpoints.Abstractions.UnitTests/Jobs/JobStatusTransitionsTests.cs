using AsyncEndpoints.Abstractions.Jobs;

namespace AsyncEndpoints.Abstractions.UnitTests.Jobs;

public class JobStatusTransitionsTests
{
	[Theory]
	[InlineData(JobStatus.Queued, JobStatus.Processing)]
	[InlineData(JobStatus.Queued, JobStatus.Cancelled)]
	[InlineData(JobStatus.Processing, JobStatus.Completed)]
	[InlineData(JobStatus.Processing, JobStatus.Failed)]
	[InlineData(JobStatus.Processing, JobStatus.Cancelled)]
	[InlineData(JobStatus.Failed, JobStatus.Queued)]
	[InlineData(JobStatus.Failed, JobStatus.DeadLettered)]
	[InlineData(JobStatus.Failed, JobStatus.Cancelled)]
	[InlineData(JobStatus.Queued, JobStatus.Queued)]
	[InlineData(JobStatus.Processing, JobStatus.Processing)]
	[InlineData(JobStatus.Completed, JobStatus.Completed)]
	[InlineData(JobStatus.Failed, JobStatus.Failed)]
	[InlineData(JobStatus.DeadLettered, JobStatus.DeadLettered)]
	[InlineData(JobStatus.Cancelled, JobStatus.Cancelled)]
	public void AssertTransitionAllowed_ValidTransitions_ShouldSucceed(JobStatus from, JobStatus to)
	{
		// Act & Assert
		JobStatusTransitions.AssertTransitionAllowed(from, to);
	}

	[Theory]
	[InlineData(JobStatus.Completed, JobStatus.Processing)]
	[InlineData(JobStatus.Queued, JobStatus.Completed)]
	[InlineData(JobStatus.Completed, JobStatus.Queued)]
	[InlineData(JobStatus.DeadLettered, JobStatus.Queued)]
	[InlineData(JobStatus.Cancelled, JobStatus.Queued)]
	public void AssertTransitionAllowed_InvalidTransitions_ShouldThrow(JobStatus from, JobStatus to)
	{
		// Act & Assert
		Assert.Throws<InvalidJobStatusTransitionException>(() =>
			JobStatusTransitions.AssertTransitionAllowed(from, to));
	}

	[Fact]
	public void AssertTransitionAllowed_ExhaustiveMatrixTest()
	{
		var allStatuses = Enum.GetValues<JobStatus>();

		foreach (var from in allStatuses)
		{
			foreach (var to in allStatuses)
			{
				bool isAllowed = JobStatusTransitions.IsTransitionAllowed(from, to);

				if (isAllowed)
				{
					JobStatusTransitions.AssertTransitionAllowed(from, to);
				}
				else
				{
					Assert.Throws<InvalidJobStatusTransitionException>(() =>
						JobStatusTransitions.AssertTransitionAllowed(from, to));
				}
			}
		}
	}
}
