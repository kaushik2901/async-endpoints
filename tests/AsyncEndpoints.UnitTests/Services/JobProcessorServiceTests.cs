using AsyncEndpoints.Contracts;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Serialization;
using AsyncEndpoints.Services;
using AsyncEndpoints.UnitTests.TestSupport;
using AsyncEndpoints.Utilities;
using AutoFixture.Xunit2;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.UnitTests.Services;

public class JobProcessorServiceTests
{
	[Theory, AutoMoqData]
	public void Constructor_Succeeds_WithValidDependencies(
		Mock<ILogger<JobProcessorService>> mockLogger,
		Mock<IJobManager> mockJobManager,
		Mock<IHandlerExecutionService> mockHandlerExecutionService,
		Mock<ISerializer> mockSerializer)
	{
		// Act
		var service = new JobProcessorService(mockLogger.Object, mockJobManager.Object, mockHandlerExecutionService.Object, mockSerializer.Object);

		// Assert
		Assert.NotNull(service);
	}

	// TODO: Fix it later
	//[Theory, AutoMoqData]
	//public async Task ProcessAsync_ExecutesHandlerAndReturnsSuccess_WhenHandlerExecutesSuccessfully(
	//    [Frozen] Mock<ILogger<JobProcessorService>> mockLogger,
	//    [Frozen] Mock<IJobManager> mockJobManager,
	//    [Frozen] Mock<IHandlerExecutionService> mockHandlerExecutionService,
	//    Job job)
	//{
	//    // Arrange
	//    var jsonOptions = Options.Create(new JsonOptions());
	//    var handlerResult = MethodResult<object>.Success("Test result");

	//    mockHandlerExecutionService
	//        .Setup(x => x.ExecuteHandlerAsync(job.Name, It.IsAny<object>(), job, It.IsAny<CancellationToken>()))
	//        .ReturnsAsync(handlerResult);

	//    var jobProcessorService = new JobProcessorService(mockLogger.Object, mockJobManager.Object, mockHandlerExecutionService.Object, jsonOptions);

	//    // Act & Assert - Should not throw exception
	//    var exception = await Record.ExceptionAsync(() => 
	//        jobProcessorService.ProcessAsync(job, CancellationToken.None));

	//    Assert.Null(exception);
	//    // We expect success path to be called
	//    mockJobManager.Verify(x => x.ProcessJobSuccess(job.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
	//}

	[Theory, AutoMoqData]
	public async Task ProcessAsync_ProcessesFailure_WhenHandlerExecutionFails(
		[Frozen] Mock<ILogger<JobProcessorService>> mockLogger,
		[Frozen] Mock<IJobManager> mockJobManager,
		[Frozen] Mock<IHandlerExecutionService> mockHandlerExecutionService,
		Job job)
	{
		// Arrange
		var handlerResult = MethodResult<object>.Failure("Handler error");

		mockHandlerExecutionService
			.Setup(x => x.ExecuteHandlerAsync(job.Name, It.IsAny<object>(), job, It.IsAny<CancellationToken>()))
			.ReturnsAsync(handlerResult);

		var jobProcessorService = new JobProcessorService(mockLogger.Object, mockJobManager.Object, mockHandlerExecutionService.Object, new Mock<ISerializer>().Object);

		// Act & Assert - Should not throw exception
		var exception = await Record.ExceptionAsync(() =>
			jobProcessorService.ProcessAsync(job, CancellationToken.None));

		Assert.Null(exception);
		mockJobManager.Verify(x => x.ProcessJobFailure(job.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
	}
}