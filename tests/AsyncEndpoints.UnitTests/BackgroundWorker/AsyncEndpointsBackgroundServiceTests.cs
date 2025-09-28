using AsyncEndpoints.BackgroundWorker;
using AsyncEndpoints.Contracts;
using AsyncEndpoints.Services;
using AsyncEndpoints.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AsyncEndpoints.UnitTests.BackgroundWorker;

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
        var configurations = Options.Create(new AsyncEndpointsConfigurations());

        // Act
        var service = new AsyncEndpointsBackgroundService(
            mockLogger.Object,
            configurations,
            mockJobProducerService.Object,
            mockJobConsumerService.Object,
            mockDateTimeProvider.Object);

        // Assert
        Assert.NotNull(service);
    }
}