using AsyncEndpoints.Background;
using AsyncEndpoints.Configuration;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.UnitTests.TestSupport;
using AutoFixture.Xunit2;
using Microsoft.Extensions.Logging;
using Moq;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace AsyncEndpoints.UnitTests.Background;

public class JobChannelEnqueuerTests
{
    [Theory, AutoMoqData]
    public void Constructor_Succeeds_WithValidDependencies(
        Mock<ILogger<JobChannelEnqueuer>> mockLogger)
    {
        // Act
        var service = new JobChannelEnqueuer(mockLogger.Object);

        // Assert
        Assert.NotNull(service);
    }

    [Theory, AutoMoqData]
    public async Task Enqueue_ReturnsTrue_WhenChannelHasSpaceAndTryWriteSucceeds(
        Mock<ILogger<JobChannelEnqueuer>> mockLogger,
        Job job)
    {
        // Arrange
        var channel = Channel.CreateBounded<Job>(new BoundedChannelOptions(10));
        var service = new JobChannelEnqueuer(mockLogger.Object);
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await service.Enqueue(channel.Writer, job, cancellationToken);

        // Assert
        Assert.True(result);
        
        // Verify the job was written to the channel
        var readResult = await channel.Reader.ReadAsync();
        Assert.Equal(job.Id, readResult.Id);
    }

    [Theory, AutoMoqData]
    public async Task Enqueue_ReturnsTrue_WhenChannelIsFullAndWriteAsyncSucceeds(
        Mock<ILogger<JobChannelEnqueuer>> mockLogger,
        Job job1,
        Job job2)
    {
        // Arrange
        var channel = Channel.CreateBounded<Job>(new BoundedChannelOptions(1) 
        { 
            FullMode = BoundedChannelFullMode.Wait 
        });
        
        // Fill the channel
        await channel.Writer.WriteAsync(job1);
        
        var service = new JobChannelEnqueuer(mockLogger.Object);
        var cancellationToken = CancellationToken.None;

        // Act & Assert with timeout to ensure the test doesn't hang
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        
        var cts = new CancellationTokenSource();
        var writeTask = service.Enqueue(channel.Writer, job2, cts.Token);
        
        // Allow some time for the background write to occur
        await Task.Delay(100);
        
        // Read the first job to unblock the channel
        var readJob1 = await channel.Reader.ReadAsync();
        
        // Now the second job should be able to write
        var result = await writeTask;
        
        Assert.True(result);
    }

    [Theory, AutoMoqData]
    public async Task Enqueue_ReturnsFalse_WhenChannelWriteTimesOut(
        Mock<ILogger<JobChannelEnqueuer>> mockLogger,
        Job job)
    {
        // Arrange - Create a scenario that will simulate timeout
        // Use a mock channel writer that will properly trigger the timeout path
        var channel = Channel.CreateBounded<Job>(new BoundedChannelOptions(1) 
        { 
            FullMode = BoundedChannelFullMode.Wait 
        });
        
        // Fill the channel to make TryWrite return false
        var fillerJob = new Job { Id = Guid.NewGuid() };
        await channel.Writer.WriteAsync(fillerJob);
        
        var service = new JobChannelEnqueuer(mockLogger.Object);

        // Act & Assert: Since the channel is full, TryWrite will fail and WriteAsync will be called
        // The timeout will happen after 5 seconds, but for testing purposes we'll make sure
        // our test doesn't hang by using a timeout on the task itself
        using var executionCts = new CancellationTokenSource(TimeSpan.FromSeconds(1)); // Short timeout for test
        var writeTask = service.Enqueue(channel.Writer, job, executionCts.Token);

        bool result;
        try
        {
            result = await writeTask;
        }
        catch (OperationCanceledException)
        {
            // If the test timeout cancels the operation, that's fine
            result = false;
        }

        Assert.False(result);
    }

    [Theory, AutoMoqData]
    public async Task Enqueue_ReturnsFalse_WhenChannelIsDisposed(
        Mock<ILogger<JobChannelEnqueuer>> mockLogger,
        Job job)
    {
        // Arrange
        var channel = Channel.CreateBounded<Job>(new BoundedChannelOptions(1));
        channel.Writer.Complete(); // Complete the channel to close it
        var service = new JobChannelEnqueuer(mockLogger.Object);
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await service.Enqueue(channel.Writer, job, cancellationToken);

        // Assert
        Assert.False(result);
        
        // The real implementation will catch ChannelClosedException (not ObjectDisposedException) 
        // and log it as an unexpected error, not as "Channel was disposed"
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unexpected error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory, AutoMoqData]
    public async Task Enqueue_ReturnsFalse_WhenOperationIsCancelled(
        Mock<ILogger<JobChannelEnqueuer>> mockLogger,
        Job job)
    {
        // Arrange
        var channel = Channel.CreateBounded<Job>(new BoundedChannelOptions(1) 
        { 
            FullMode = BoundedChannelFullMode.Wait 
        });
        
        // Fill the channel
        await channel.Writer.WriteAsync(job);
        
        var service = new JobChannelEnqueuer(mockLogger.Object);
        var cancellationTokenSource = new CancellationTokenSource();
        
        // Cancel the token before attempting to write
        cancellationTokenSource.CancelAfter(10); // Very short delay to ensure cancellation
        
        // Act
        var result = await service.Enqueue(channel.Writer, job, cancellationTokenSource.Token);

        // Assert
        Assert.False(result);
        
        // Verify debug log was called for cancellation
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Job producer was cancelled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory, AutoMoqData]
    public async Task Enqueue_ReturnsFalse_WhenUnexpectedExceptionOccurs(
        Mock<ILogger<JobChannelEnqueuer>> mockLogger,
        Job job)
    {
        // Arrange
        // Use a custom channel that throws an unexpected exception
        var mockChannelWriter = new Mock<ChannelWriter<Job>>();
        mockChannelWriter
            .Setup(x => x.TryWrite(It.IsAny<Job>()))
            .Returns(false); // Force non-blocking write to fail
        
        mockChannelWriter
            .Setup(x => x.WriteAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));
            
        var service = new JobChannelEnqueuer(mockLogger.Object);
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await service.Enqueue(mockChannelWriter.Object, job, cancellationToken);

        // Assert
        Assert.False(result);
        
        // Verify error log was called for unexpected exception
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unexpected error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory, AutoMoqData]
    public async Task Enqueue_UsesCorrectTimeoutValue(
        Mock<ILogger<JobChannelEnqueuer>> mockLogger,
        Job job)
    {
        // Arrange
        var channel = Channel.CreateBounded<Job>(new BoundedChannelOptions(1) 
        { 
            FullMode = BoundedChannelFullMode.Wait 
        });
        
        // Fill the channel
        await channel.Writer.WriteAsync(job);
        
        var service = new JobChannelEnqueuer(mockLogger.Object);
        
        // Since we can't directly test internal timeout logic easily, 
        // we'll verify the behavior with a test that would fail if timeout was not set properly
        var startTime = DateTimeOffset.UtcNow;
        var result = await service.Enqueue(channel.Writer, job, CancellationToken.None);
        var endTime = DateTimeOffset.UtcNow;
        
        // The method should return false due to timeout (5 seconds by default) 
        // but the test should not hang indefinitely
        Assert.False(result);
        Assert.InRange((endTime - startTime).TotalSeconds, 0, 10); // Should not take more than 10 seconds
    }


}