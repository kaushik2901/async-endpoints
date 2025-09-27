using AsyncEndpoints.Entities;
using AsyncEndpoints.Services;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using AutoFixture.Xunit2;
using Moq;

namespace AsyncEndpoints.UnitTests;

public class JobConsumerServiceTests
{
    [Theory, AutoMoqData]
    public void Constructor_Succeeds_WithValidDependencies(
        Mock<ILogger<JobConsumerService>> mockLogger,
        Mock<IJobProcessorService> mockJobProcessorService)
    {
        // Act
        var service = new JobConsumerService(mockLogger.Object, mockJobProcessorService.Object);

        // Assert
        Assert.NotNull(service);
    }

    [Theory, AutoMoqData]
    public async Task ConsumeJobsAsync_ProcessesAvailableJobs(
        [Frozen] Mock<ILogger<JobConsumerService>> mockLogger,
        [Frozen] Mock<IJobProcessorService> mockJobProcessorService,
        JobConsumerService jobConsumerService,
        SemaphoreSlim semaphoreSlim,
        Job job)
    {
        // Arrange
        var channel = Channel.CreateBounded<Job>(new BoundedChannelOptions(10));
        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)); // Short timeout
        await channel.Writer.WriteAsync(job, CancellationToken.None);
        channel.Writer.Complete();

        // Act & Assert - Should not throw exception
        var exception = await Record.ExceptionAsync(() => 
            jobConsumerService.ConsumeJobsAsync(channel.Reader, semaphoreSlim, cancellationTokenSource.Token));

        Assert.Null(exception);
        mockJobProcessorService.Verify(x => x.ProcessAsync(job, It.IsAny<CancellationToken>()), Times.Once);
    }
}