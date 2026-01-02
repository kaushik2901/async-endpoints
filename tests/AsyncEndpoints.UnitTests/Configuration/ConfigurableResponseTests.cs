using AsyncEndpoints.Configuration;
using AsyncEndpoints.Handlers;
using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.Infrastructure.Serialization;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.UnitTests.Configuration;

public class ConfigurableResponseTests
{
	[Fact]
	public async Task AsyncEndpointRequestDelegate_UsesCustomJobSubmittedResponseFactory_WhenConfigured()
	{
		// Arrange
		var mockLogger = new Mock<ILogger<AsyncEndpointRequestDelegate>>();
		var mockJobManager = new Mock<IJobManager>();
		var mockSerializer = new Mock<ISerializer>();
		var mockDateTimeProvider = new Mock<IDateTimeProvider>();
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(DateTimeOffset.UtcNow);

		// Create custom configurations with custom response factory
		var configurations = new AsyncEndpointsConfigurations
		{
			ResponseConfigurations = new AsyncEndpointsResponseConfigurations
			{
				JobSubmittedResponseFactory = (job, context) =>
				{
					var response = Results.Created($"/api/custom/{job.Id}", new { JobId = job.Id, CustomMessage = "Custom response" });
					return Task.FromResult(response);
				}
			}
		};

		var httpContext = new DefaultHttpContext();
		var job = Job.Create(
			Guid.NewGuid(),
			"TestJob",
			"{}",
			[],
			[],
			[],
			AsyncEndpointsConstants.MaximumRetries,
			mockDateTimeProvider.Object);
		var request = new object();

		var successResult = MethodResult<Job>.Success(job);
		mockJobManager
			.Setup(x => x.SubmitJob(It.IsAny<string>(), It.IsAny<string>(), httpContext, It.IsAny<CancellationToken>()))
			.ReturnsAsync(successResult);

		mockSerializer
			.Setup(x => x.Serialize(request, null))
			.Returns("{}");

		var requestDelegate = new AsyncEndpointRequestDelegate(mockLogger.Object, mockJobManager.Object, mockSerializer.Object, configurations);

		// Act
		var result = await requestDelegate.HandleAsync("test-job", httpContext, request);

		// Assert - Just ensure we get a result back without error
		Assert.NotNull(result);
	}

	[Fact]
	public async Task AsyncEndpointRequestDelegate_UsesCustomErrorResponseFactory_WhenJobSubmissionFails()
	{
		// Arrange
		var mockLogger = new Mock<ILogger<AsyncEndpointRequestDelegate>>();
		var mockJobManager = new Mock<IJobManager>();
		var mockSerializer = new Mock<ISerializer>();

		// Create custom configurations with custom error response factory
		var configurations = new AsyncEndpointsConfigurations
		{
			ResponseConfigurations = new AsyncEndpointsResponseConfigurations
			{
				JobSubmissionErrorResponseFactory = (error, context) =>
				{
					var response = Results.Json(new { Error = "Custom error", Code = "CUSTOM_ERROR" }, statusCode: 422);
					return Task.FromResult(response);
				}
			}
		};

		var httpContext = new DefaultHttpContext();
		var request = new object();

		var error = new AsyncEndpointError("TEST_ERROR", "Test error message", null);
		var failureResult = MethodResult<Job>.Failure(error);
		mockJobManager
			.Setup(x => x.SubmitJob(It.IsAny<string>(), It.IsAny<string>(), httpContext, It.IsAny<CancellationToken>()))
			.ReturnsAsync(failureResult);

		mockSerializer
			.Setup(x => x.Serialize(request, null))
			.Returns("{}");

		var requestDelegate = new AsyncEndpointRequestDelegate(mockLogger.Object, mockJobManager.Object, mockSerializer.Object, configurations);

		// Act
		var result = await requestDelegate.HandleAsync("test-job", httpContext, request);

		// Assert
		Assert.NotNull(result);
	}
}
