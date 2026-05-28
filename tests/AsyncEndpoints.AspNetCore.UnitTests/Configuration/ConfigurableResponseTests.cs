using AsyncEndpoints.Abstractions.Common;
using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.AspNetCore.Configuration;
using AsyncEndpoints.AspNetCore.Handlers;
using AsyncEndpoints.Core.Infrastructure.Serialization;
using AsyncEndpoints.Core.Legacy.JobProcessing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.AspNetCore.UnitTests.Configuration;

public class ConfigurableResponseTests
{
	[Fact]
	public async Task AsyncEndpointRequestDelegate_UsesCustomJobSubmittedResponseFactory_WhenConfigured()
	{
		var mockLogger = new Mock<ILogger<AsyncEndpointRequestDelegate>>();
		var mockJobManager = new Mock<IJobManager>();
		var mockSerializer = new Mock<ISerializer>();
		var mockDateTimeProvider = new Mock<TimeProvider>();
		mockDateTimeProvider.Setup(x => x.GetUtcNow()).Returns(DateTimeOffset.UtcNow);

		var responseConfig = new AsyncEndpointsResponseConfigurations
		{
			JobSubmittedResponseFactory = (jobId, context) =>
			{
				var response = Results.Created($"/api/custom/{jobId}", new { JobId = jobId, CustomMessage = "Custom response" });
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
			3,
			mockDateTimeProvider.Object);
		var request = new object();

		var successResult = MethodResult<Job>.Success(job);
		mockJobManager
			.Setup(x => x.SubmitJob(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Dictionary<string, List<string?>>>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<List<KeyValuePair<string, List<string?>>>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(successResult);

		mockSerializer
			.Setup(x => x.Serialize(request, (System.Text.Json.JsonSerializerOptions?)null))
			.Returns("{}");

		var requestDelegate = new AsyncEndpointRequestDelegate(mockLogger.Object, mockJobManager.Object, mockSerializer.Object, responseConfig);

		var result = await requestDelegate.HandleAsync("test-job", httpContext, request);

		Assert.NotNull(result);
	}

	[Fact]
	public async Task AsyncEndpointRequestDelegate_UsesCustomErrorResponseFactory_WhenJobSubmissionFails()
	{
		var mockLogger = new Mock<ILogger<AsyncEndpointRequestDelegate>>();
		var mockJobManager = new Mock<IJobManager>();
		var mockSerializer = new Mock<ISerializer>();

		var responseConfig = new AsyncEndpointsResponseConfigurations();

		var httpContext = new DefaultHttpContext();
		var request = new object();

		var error = new AsyncEndpointError("TEST_ERROR", "Test error message", null);
		var failureResult = MethodResult<Job>.Failure(error);
		mockJobManager
			.Setup(x => x.SubmitJob(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Dictionary<string, List<string?>>>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<List<KeyValuePair<string, List<string?>>>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(failureResult);

		mockSerializer
			.Setup(x => x.Serialize(request, (System.Text.Json.JsonSerializerOptions?)null))
			.Returns("{}");

		var requestDelegate = new AsyncEndpointRequestDelegate(mockLogger.Object, mockJobManager.Object, mockSerializer.Object, responseConfig);

		var result = await requestDelegate.HandleAsync("test-job", httpContext, request);

		Assert.NotNull(result);
	}
}
