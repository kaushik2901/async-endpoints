namespace AsyncEndpoints.UnitTests;

public class AsyncEndpointsConstantsTests
{
    [Fact]
    public void AsyncEndpointTag_IsCorrect()
    {
        Assert.Equal("AsyncEndpoint", AsyncEndpointsConstants.AsyncEndpointTag);
    }

    [Fact]
    public void JobIdHeaderName_IsCorrect()
    {
        Assert.Equal("Async-Job-Id", AsyncEndpointsConstants.JobIdHeaderName);
    }

    [Fact]
    public void MaximumRetries_IsCorrect()
    {
        Assert.Equal(3, AsyncEndpointsConstants.MaximumRetries);
    }

    [Fact]
    public void DefaultPollingIntervalMs_IsCorrect()
    {
        Assert.Equal(1000, AsyncEndpointsConstants.DefaultPollingIntervalMs);
    }

    [Fact]
    public void DefaultJobTimeoutMinutes_IsCorrect()
    {
        Assert.Equal(30, AsyncEndpointsConstants.DefaultJobTimeoutMinutes);
    }

    [Fact]
    public void DefaultBatchSize_IsCorrect()
    {
        Assert.Equal(5, AsyncEndpointsConstants.DefaultBatchSize);
    }

    [Fact]
    public void DefaultMaximumQueueSize_IsCorrect()
    {
        Assert.Equal(50, AsyncEndpointsConstants.DefaultMaximumQueueSize);
    }

    [Fact]
    public void BackgroundServiceShutdownTimeoutSeconds_IsCorrect()
    {
        Assert.Equal(30, AsyncEndpointsConstants.BackgroundServiceShutdownTimeoutSeconds);
    }

    [Fact]
    public void BackgroundServiceWaitDelayMs_IsCorrect()
    {
        Assert.Equal(100, AsyncEndpointsConstants.BackgroundServiceWaitDelayMs);
    }

    [Fact]
    public void JobProducerMaxDelayMs_IsCorrect()
    {
        Assert.Equal(30000, AsyncEndpointsConstants.JobProducerMaxDelayMs);
    }

    [Fact]
    public void JobProducerErrorDelaySeconds_IsCorrect()
    {
        Assert.Equal(5, AsyncEndpointsConstants.JobProducerErrorDelaySeconds);
    }

    [Fact]
    public void JobProducerChannelWriteTimeoutSeconds_IsCorrect()
    {
        Assert.Equal(5, AsyncEndpointsConstants.JobProducerChannelWriteTimeoutSeconds);
    }

    [Fact]
    public void RetryDelayBaseSeconds_IsCorrect()
    {
        Assert.Equal(5, AsyncEndpointsConstants.RetryDelayBaseSeconds);
    }

    [Fact]
    public void ExceptionSeparatorLength_IsCorrect()
    {
        Assert.Equal(50, AsyncEndpointsConstants.ExceptionSeparatorLength);
    }
}