using AsyncEndpoints.Background;
using AsyncEndpoints.UnitTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.UnitTests.Background;

public class HandlerExecutionServiceTests
{
	[Theory, AutoMoqData]
	public void Constructor_Succeeds_WithValidDependencies(
		Mock<ILogger<HandlerExecutionService>> mockLogger,
		IServiceScopeFactory serviceScopeFactory)
	{
		// Act
		var service = new HandlerExecutionService(mockLogger.Object, serviceScopeFactory);

		// Assert
		Assert.NotNull(service);
	}

	// TODO: Fix it later
	//[Theory, AutoMoqData]
	//public async Task ExecuteHandlerAsync_ReturnsFailure_WhenHandlerDoesNotExist(
	//    [Frozen] Mock<ILogger<HandlerExecutionService>> mockLogger,
	//    [Frozen] Mock<IServiceScopeFactory> mockServiceScopeFactory,
	//    string jobName,
	//    object request,
	//    Job job)
	//{
	//    // Arrange
	//    var mockServiceProvider = new Mock<IServiceProvider>();
	//    var mockServiceScope = Mock.Of<IAsyncServiceScope>(x => x.ServiceProvider == mockServiceProvider.Object);

	//    mockServiceScopeFactory
	//        .Setup(x => x.CreateAsyncScope())
	//        .Returns(mockServiceScope);

	//    var handlerExecutionService = new HandlerExecutionService(mockLogger.Object, mockServiceScopeFactory.Object);

	//    // Act
	//    var result = await handlerExecutionService.ExecuteHandlerAsync(jobName, request, job, CancellationToken.None);

	//    // Assert
	//    Assert.False(result.IsSuccess);
	//    Assert.NotNull(result.Error);
	//    Assert.Contains(jobName, result.Error!.Message);
	//}
}
