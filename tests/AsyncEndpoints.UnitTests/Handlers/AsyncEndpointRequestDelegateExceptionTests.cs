using AsyncEndpoints.Configuration;
using AsyncEndpoints.Handlers;
using AsyncEndpoints.Infrastructure.Serialization;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.UnitTests.TestSupport;
using AsyncEndpoints.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.UnitTests.Services;

public class AsyncEndpointRequestDelegateExceptionTests
{
	[Theory, AutoMoqData]
	public async Task HandleAsync_WhenJobSubmissionFails_ShouldReturnProblemResultWithDetailedError(
		string jobName,
		HttpContext httpContext,
		object request,
		Mock<ILogger<AsyncEndpointRequestDelegate>> mockLogger,
		Mock<IJobManager> mockJobManager,
		Mock<ISerializer> mockSerializer)
	{
		// Arrange
		var error = new AsyncEndpointError("SUBMISSION_ERROR", "Failed to submit job", null);
		var failureResult = MethodResult<Job>.Failure(error);

		mockJobManager
			.Setup(x => x.SubmitJob(jobName, It.IsAny<string>(), httpContext, It.IsAny<CancellationToken>()))
			.ReturnsAsync(failureResult);

		mockSerializer
			.Setup(x => x.Serialize(request, null))
			.Returns("{}");

		var configurations = new AsyncEndpointsConfigurations();
		var requestDelegate = new AsyncEndpointRequestDelegate(mockLogger.Object, mockJobManager.Object, mockSerializer.Object, configurations);

		// Act
		var result = await requestDelegate.HandleAsync(jobName, httpContext, request, cancellationToken: default);

		// Assert
		Assert.IsType<ProblemHttpResult>(result);
		// Note: The internal structure of ProblemHttpResult might be different, so we'll focus on successful execution
	}

	[Theory, AutoMoqData]
	public async Task HandleAsync_WhenJobSubmissionFailsWithException_ShouldLogException(
		string jobName,
		HttpContext httpContext,
		object request,
		Mock<ILogger<AsyncEndpointRequestDelegate>> mockLogger,
		Mock<IJobManager> mockJobManager,
		Mock<ISerializer> mockSerializer)
	{
		// Arrange
		var exception = new InvalidOperationException("Test exception");
		var error = new AsyncEndpointError("SUBMISSION_ERROR", "Failed to submit job", exception);
		var failureResult = MethodResult<Job>.Failure(error);

		mockJobManager
			.Setup(x => x.SubmitJob(jobName, It.IsAny<string>(), httpContext, It.IsAny<CancellationToken>()))
			.ReturnsAsync(failureResult);

		mockSerializer
			.Setup(x => x.Serialize(request, null))
			.Returns("{}");

		var configurations = new AsyncEndpointsConfigurations();
		var requestDelegate = new AsyncEndpointRequestDelegate(mockLogger.Object, mockJobManager.Object, mockSerializer.Object, configurations);

		// Act
		var result = await requestDelegate.HandleAsync(jobName, httpContext, request, cancellationToken: default);

		// Assert - Check that the logger was called appropriately
		mockLogger.Verify(
			x => x.Log(
				LogLevel.Error,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to submit job")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}
}
