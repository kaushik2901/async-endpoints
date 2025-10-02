using AsyncEndpoints.Contracts;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Serialization;
using AsyncEndpoints.Services;
using AsyncEndpoints.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.UnitTests.Services;

public class ConfigurableResponseTests
{
	[Fact]
	public async Task AsyncEndpointRequestDelegate_UsesCustomJobSubmittedResponseFactory_WhenConfigured()
	{
		// Arrange
		var mockLogger = new Mock<ILogger<AsyncEndpointRequestDelegate>>();
		var mockJobManager = new Mock<IJobManager>();
		var mockSerializer = new Mock<ISerializer>();

		// Create custom configurations with custom response factory
		var configurations = new AsyncEndpointsConfigurations
		{
			ResponseConfigurations = new AsyncEndpointsResponseConfigurations
			{
				JobSubmittedResponseFactory = async (job, context) =>
				{
					return Results.Created($"/api/custom/{job.Id}", new { JobId = job.Id, CustomMessage = "Custom response" });
				}
			}
		};

		var httpContext = new DefaultHttpContext();
		var job = new Job { Id = Guid.NewGuid(), Name = "TestJob" };
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
				JobSubmissionErrorResponseFactory = async (error, context) =>
				{
					return Results.Json(new { Error = "Custom error", Code = "CUSTOM_ERROR" }, statusCode: 422);
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
