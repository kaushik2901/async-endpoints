using System.Threading.Channels;
using AsyncEndpoints.Background;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.UnitTests.TestSupport;
using AutoFixture.Xunit2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.UnitTests.Services;

public class JobConsumerServiceTests
{
	[Theory, AutoMoqData]
	public void Constructor_Succeeds_WithValidDependencies(
		[Frozen] Mock<IServiceScopeFactory> mockScopeFactory,
		Mock<ILogger<JobConsumerService>> mockLogger)
	{
		// Act
		var service = new JobConsumerService(mockLogger.Object, mockScopeFactory.Object);

		// Assert
		Assert.NotNull(service);
	}

	[Theory, AutoMoqData]
	public async Task ConsumeJobsAsync_ProcessesAvailableJobs(
		[Frozen] Mock<IServiceProvider> mockServiceProvider,
		[Frozen] Mock<IServiceScope> mockScope,
		[Frozen] Mock<IServiceScopeFactory> mockScopeFactory,
		[Frozen] Mock<IJobProcessorService> mockJobProcessorService,
		[Frozen] Mock<ILogger<JobConsumerService>> mockLogger,
		SemaphoreSlim semaphoreSlim,
		Job job)
	{
		// Arrange
		mockServiceProvider
			.Setup(x => x.GetService(typeof(IJobProcessorService)))
			.Returns(mockJobProcessorService.Object);

		mockScope
			.Setup(x => x.ServiceProvider)
			.Returns(mockServiceProvider.Object);

		mockScopeFactory
			.Setup(x => x.CreateScope())
			.Returns(mockScope.Object);

		var channel = Channel.CreateBounded<Job>(new BoundedChannelOptions(10));
		var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)); // Short timeout
		await channel.Writer.WriteAsync(job, CancellationToken.None);
		channel.Writer.Complete();

		var service = new JobConsumerService(mockLogger.Object, mockScopeFactory.Object);

		// Act & Assert - Should not throw exception
		var exception = await Record.ExceptionAsync(() =>
			service.ConsumeJobsAsync(channel.Reader, semaphoreSlim, cancellationTokenSource.Token));

		Assert.Null(exception);
		mockJobProcessorService.Verify(x => x.ProcessAsync(job, It.IsAny<CancellationToken>()), Times.Once);
	}
}
