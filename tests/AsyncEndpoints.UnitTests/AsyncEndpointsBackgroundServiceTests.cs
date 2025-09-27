using AsyncEndpoints.BackgroundWorker;
using AsyncEndpoints.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AsyncEndpoints.UnitTests;

public class AsyncEndpointsBackgroundServiceTests
{
    [Theory, AutoMoqData]
    public void Constructor_Succeeds_WithValidDependencies(
        Mock<ILogger<AsyncEndpointsBackgroundService>> mockLogger,
        Mock<IJobProducerService> mockJobProducerService,
        Mock<IJobConsumerService> mockJobConsumerService)
    {
        // Arrange
        var configurations = Options.Create(new AsyncEndpointsConfigurations());

        // Act
        var service = new AsyncEndpointsBackgroundService(
            mockLogger.Object, 
            configurations, 
            mockJobProducerService.Object, 
            mockJobConsumerService.Object);

        // Assert
        Assert.NotNull(service);
    }
}