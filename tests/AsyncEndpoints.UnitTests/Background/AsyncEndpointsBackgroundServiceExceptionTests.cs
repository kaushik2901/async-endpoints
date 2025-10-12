using System.Threading.Channels;
using AsyncEndpoints.Background;
using AsyncEndpoints.Configuration;
using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AsyncEndpoints.UnitTests.Background;

public class AsyncEndpointsBackgroundServiceExceptionTests
{
	[Theory, AutoMoqData]
	public async Task ExecuteAsync_WhenProducerTaskFails_ShouldLogErrorAndRethrow(
		Mock<ILogger<AsyncEndpointsBackgroundService>> mockLogger,
		Mock<IOptions<AsyncEndpointsConfigurations>> mockOptions,
		Mock<IJobProducerService> mockJobProducerService,
		Mock<IJobConsumerService> mockJobConsumerService,
		Mock<IDateTimeProvider> mockDateTimeProvider,
		CancellationToken stoppingToken)
	{
		// Arrange
		var expectedException = new InvalidOperationException("Producer task failed");

		mockJobProducerService
			.Setup(x => x.ProduceJobsAsync(It.IsAny<ChannelWriter<Job>>(), stoppingToken))
			.Returns(Task.FromException(expectedException));

		mockJobConsumerService
			.Setup(x => x.ConsumeJobsAsync(It.IsAny<ChannelReader<Job>>(), It.IsAny<SemaphoreSlim>(), stoppingToken))
			.Returns(Task.CompletedTask);

		var configurations = new AsyncEndpointsConfigurations
		{
			WorkerConfigurations = new AsyncEndpointsWorkerConfigurations
			{
				MaximumConcurrency = 2,
				MaximumQueueSize = 100,
				BatchSize = 10,
				PollingIntervalMs = 1000
			}
		};

		mockOptions.Setup(x => x.Value).Returns(configurations);

		var backgroundService = new TestableBackgroundService(
			mockLogger.Object,
			mockOptions.Object,
			mockJobProducerService.Object,
			mockJobConsumerService.Object,
			mockDateTimeProvider.Object);

		// Act & Assert
		var exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => backgroundService.ExecuteAsyncForTest(stoppingToken));

		Assert.Equal(expectedException, exception);
	}

	// Helper class to test the protected ExecuteAsync method
	private class TestableBackgroundService : AsyncEndpointsBackgroundService
	{
		public TestableBackgroundService(
			ILogger<AsyncEndpointsBackgroundService> logger,
			IOptions<AsyncEndpointsConfigurations> configurations,
			IJobProducerService jobProducerService,
			IJobConsumerService jobConsumerService,
			IDateTimeProvider dateTimeProvider)
			: base(logger, configurations, jobProducerService, jobConsumerService, dateTimeProvider)
		{
		}

		public async Task ExecuteAsyncForTest(CancellationToken stoppingToken)
		{
			await ExecuteAsync(stoppingToken);
		}
	}
}
