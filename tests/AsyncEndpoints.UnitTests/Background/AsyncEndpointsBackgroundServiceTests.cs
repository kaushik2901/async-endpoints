using AsyncEndpoints.Background;
using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.UnitTests.TestSupport;
using AsyncEndpoints.Worker.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.UnitTests.Background;

public class AsyncEndpointsBackgroundServiceTests
{
	[Theory, AutoMoqData]
	public void Constructor_Succeeds_WithValidDependencies(
		Mock<ILogger<AsyncEndpointsBackgroundService>> mockLogger,
		Mock<IJobProducerService> mockJobProducerService,
		Mock<IJobConsumerService> mockJobConsumerService,
		Mock<IDateTimeProvider> mockDateTimeProvider)
	{
		// Arrange
		var workerOptions = new WorkerOptions();

		// Act
		var service = new AsyncEndpointsBackgroundService(
			mockLogger.Object,
			workerOptions,
			mockJobProducerService.Object,
			mockJobConsumerService.Object,
			mockDateTimeProvider.Object);

		// Assert
		Assert.NotNull(service);
	}
}
