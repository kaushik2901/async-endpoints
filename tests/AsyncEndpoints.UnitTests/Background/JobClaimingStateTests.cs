using AsyncEndpoints.Background;

namespace AsyncEndpoints.UnitTests.Background;

public class JobClaimingStateTests
{
	[Fact]
	public void JobClaimingState_ValuesAreCorrect()
	{
		// Verify that the enum values are as expected
		Assert.Equal(100, (int)JobClaimingState.JobSuccessfullyEnqueued);
		Assert.Equal(200, (int)JobClaimingState.NoJobFound);
		Assert.Equal(300, (int)JobClaimingState.FailedToEnqueue);
		Assert.Equal(400, (int)JobClaimingState.ErrorOccurred);
	}

	[Fact]
	public void JobClaimingState_HasCorrectNames()
	{
		// Verify that the enum names are correct
		Assert.Equal("JobSuccessfullyEnqueued", JobClaimingState.JobSuccessfullyEnqueued.ToString());
		Assert.Equal("NoJobFound", JobClaimingState.NoJobFound.ToString());
		Assert.Equal("FailedToEnqueue", JobClaimingState.FailedToEnqueue.ToString());
		Assert.Equal("ErrorOccurred", JobClaimingState.ErrorOccurred.ToString());
	}
}