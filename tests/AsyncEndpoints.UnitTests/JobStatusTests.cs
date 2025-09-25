using AsyncEndpoints.Entities;
using Xunit;

namespace AsyncEndpoints.UnitTests;

public class JobStatusTests
{
    [Fact]
    public void JobStatus_Values_AreCorrect()
    {
        Assert.Equal(100, (int)JobStatus.Queued);
        Assert.Equal(200, (int)JobStatus.Scheduled);
        Assert.Equal(300, (int)JobStatus.InProgress);
        Assert.Equal(400, (int)JobStatus.Completed);
        Assert.Equal(500, (int)JobStatus.Failed);
        Assert.Equal(600, (int)JobStatus.Canceled);
    }

    [Fact]
    public void JobStatus_Names_AreCorrect()
    {
        Assert.Equal("Queued", JobStatus.Queued.ToString());
        Assert.Equal("Scheduled", JobStatus.Scheduled.ToString());
        Assert.Equal("InProgress", JobStatus.InProgress.ToString());
        Assert.Equal("Completed", JobStatus.Completed.ToString());
        Assert.Equal("Failed", JobStatus.Failed.ToString());
        Assert.Equal("Canceled", JobStatus.Canceled.ToString());
    }
}