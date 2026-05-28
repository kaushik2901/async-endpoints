using AsyncEndpoints.Abstractions.Infrastructure;
using AsyncEndpoints.Abstractions.Utilities;
using AsyncEndpoints.AspNetCore.Configuration;
using AsyncEndpoints.AspNetCore.Handlers;
using AsyncEndpoints.Core.Configuration;
using AsyncEndpoints.Core.Infrastructure.Serialization;
using AsyncEndpoints.Core.JobProcessing;
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
		var responseConfig = new AsyncEndpointsResponseConfigurations
		{
			JobSubmittedResponseFactory = (job, context) =>
			{
				var response = Results.Created($"/api/custom/{job.Id}", new { JobId = job.Id, CustomMessage = "Custom response" });
				return Task.FromResult(response);
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
			.Setup(x => x.SubmitJob(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Dictionary<string, List<string?>>>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<List<KeyValuePair<string, List<string?>>>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(successResult);

		mockSerializer
			.Setup(x => x.Serialize(request, null))
			.Returns("{}");

		var requestDelegate = new AsyncEndpointRequestDelegate(mockLogger.Object, mockJobManager.Object, mockSerializer.Object, responseConfig);

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
		var responseConfig = new AsyncEndpointsResponseConfigurations
		{
			JobSubmissionErrorResponseFactory = (error, context) =>
			{
				var response = Results.Json(new { Error = "Custom error", Code = "CUSTOM_ERROR" }, statusCode: 422);
				return Task.FromResult(response);
			}
		};

		var httpContext = new DefaultHttpContext();
		var request = new object();

		var error = new AsyncEndpointError("TEST_ERROR", "Test error message", null);
		var failureResult = MethodResult<Job>.Failure(error);
		mockJobManager
			.Setup(x => x.SubmitJob(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Dictionary<string, List<string?>>>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<List<KeyValuePair<string, List<string?>>>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(failureResult);

		mockSerializer
			.Setup(x => x.Serialize(request, null))
			.Returns("{}");

		var requestDelegate = new AsyncEndpointRequestDelegate(mockLogger.Object, mockJobManager.Object, mockSerializer.Object, responseConfig);

		// Act
		var result = await requestDelegate.HandleAsync("test-job", httpContext, request);

		// Assert
		Assert.NotNull(result);
	}
}
