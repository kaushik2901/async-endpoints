using AsyncEndpoints.AspNetCore.Endpoints;
using AsyncEndpoints.Core.Infrastructure.Serialization;
using AsyncEndpoints.Core.Legacy.JobProcessing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Text.Json;

namespace AsyncEndpoints.AspNetCore.UnitTests.Utilities;

public class JobResultResponseTests
{
	[Fact]
	public void Constructor_Succeeds_WithValidParameters()
	{
		var mockDateTimeProvider = new Mock<TimeProvider>();
		mockDateTimeProvider.Setup(x => x.GetUtcNow()).Returns(DateTimeOffset.UtcNow);
		var job = Job.Create(
			Guid.NewGuid(),
			"TestJob",
			"{}",
			[],
			[],
			[],
			3,
			mockDateTimeProvider.Object);
		var statusCode = 200;

		var result = new JobResultResponse(job, statusCode);

		Assert.NotNull(result);
	}

	[Fact]
	public void Constructor_UsesDefaultStatusCode_WhenNotProvided()
	{
		var mockDateTimeProvider = new Mock<TimeProvider>();
		mockDateTimeProvider.Setup(x => x.GetUtcNow()).Returns(DateTimeOffset.UtcNow);
		var job = Job.Create(
			Guid.NewGuid(),
			"TestJob",
			"{}",
			[],
			[],
			[],
			3,
			mockDateTimeProvider.Object);

		var result = new JobResultResponse(job);

		Assert.NotNull(result);
	}

	[Fact]
	public async Task ExecuteAsync_SetsCorrectResponse()
	{
		var jobId = Guid.NewGuid();
		var mockDateTimeProvider = new Mock<TimeProvider>();
		mockDateTimeProvider.Setup(x => x.GetUtcNow()).Returns(DateTimeOffset.UtcNow);
		var job = Job.Create(
			jobId,
			"TestJob",
			"{}",
			[],
			[],
			[],
			3,
			mockDateTimeProvider.Object);

		job = job.CreateCopy(
			status: JobStatus.Completed,
			result: "\"Test Result\"",
			lastUpdatedAt: DateTimeOffset.UtcNow,
			dateTimeProvider: mockDateTimeProvider.Object);

		var statusCode = 200;
		var result = new JobResultResponse(job, statusCode);

		var httpContext = new DefaultHttpContext();
		httpContext.Response.Body = new MemoryStream();

		var mockSerializer = new Mock<ISerializer>();
		var expectedSerialized = "{\"Id\":\"00000000-0000-0000-0000-000000000000\",\"Name\":\"\",\"Status\":\"\",\"Headers\":{},\"RouteParams\":{},\"QueryParams\":[],\"Payload\":\"\",\"Result\":\"__JOB_RESULT_PLACEHOLDER__\",\"Error\":null,\"RetryCount\":0,\"MaxRetries\":0,\"RetryDelayUntil\":null,\"WorkerId\":\"00000000-0000-0000-0000-000000000000\",\"CreatedAt\":\"0001-01-01T00:00:00+00:00\",\"StartedAt\":\"0001-01-01T00:00:00+00:00\",\"CompletedAt\":\"0001-01-01T00:00:00+00:00\",\"LastUpdatedAt\":\"0001-01-01T00:00:00+00:00\",\"IsCanceled\":false}";
		mockSerializer
			.Setup(x => x.Serialize(It.IsAny<object>(), It.IsAny<Type>(), It.IsAny<JsonSerializerOptions>()))
			.Returns(expectedSerialized);
		mockSerializer
			.Setup(x => x.Serialize(It.IsAny<object>(), It.IsAny<JsonSerializerOptions>()))
			.Returns(expectedSerialized);

		var serviceCollection = new ServiceCollection();
		serviceCollection.AddSingleton(mockSerializer.Object);
		var serviceProvider = serviceCollection.BuildServiceProvider();
		httpContext.RequestServices = serviceProvider;

		await result.ExecuteAsync(httpContext);

		Assert.Equal(statusCode, httpContext.Response.StatusCode);
		Assert.Equal("application/json", httpContext.Response.ContentType);

		httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
		using var reader = new StreamReader(httpContext.Response.Body);
		var responseBody = await reader.ReadToEndAsync();

		Assert.Contains("application/json", httpContext.Response.ContentType);
		Assert.NotEmpty(responseBody);
	}

	[Fact]
	public async Task ExecuteAsync_HandlesComplexJobData()
	{
		var jobId = Guid.NewGuid();
		var complexResult = new { Message = "Success", Data = new { Id = 123, Name = "Test" } };
		var jobResultJson = JsonSerializer.Serialize(complexResult);
		var mockDateTimeProvider = new Mock<TimeProvider>();
		mockDateTimeProvider.Setup(x => x.GetUtcNow()).Returns(DateTimeOffset.UtcNow);
		var job = Job.Create(
			jobId,
			"ComplexJob",
			"{}",
			[],
			[],
			[],
			3,
			mockDateTimeProvider.Object);

		job = job.CreateCopy(
			status: JobStatus.Completed,
			result: jobResultJson,
			startedAt: DateTimeOffset.UtcNow.AddMinutes(1),
			completedAt: DateTimeOffset.UtcNow.AddMinutes(2),
			retryCount: 0,
			lastUpdatedAt: DateTimeOffset.UtcNow,
			dateTimeProvider: mockDateTimeProvider.Object);

		var result = new JobResultResponse(job);

		var httpContext = new DefaultHttpContext();
		httpContext.Response.Body = new MemoryStream();

		var mockSerializer = new Mock<ISerializer>();
		var expectedSerialized = "{\"Id\":\"00000000-0000-0000-0000-000000000000\",\"Name\":\"\",\"Status\":\"\",\"Headers\":{},\"RouteParams\":{},\"QueryParams\":[],\"Payload\":\"\",\"Result\":\"__JOB_RESULT_PLACEHOLDER__\",\"Error\":null,\"RetryCount\":0,\"MaxRetries\":0,\"RetryDelayUntil\":null,\"WorkerId\":\"00000000-0000-0000-0000-000000000000\",\"CreatedAt\":\"0001-01-01T00:00:00+00:00\",\"StartedAt\":\"0001-01-01T00:00:00+00:00\",\"CompletedAt\":\"0001-01-01T00:00:00+00:00\",\"LastUpdatedAt\":\"0001-01-01T00:00:00+00:00\",\"IsCanceled\":false}";
		mockSerializer
			.Setup(x => x.Serialize(It.IsAny<object>(), It.IsAny<Type>(), It.IsAny<JsonSerializerOptions>()))
			.Returns(expectedSerialized);
		mockSerializer
			.Setup(x => x.Serialize(It.IsAny<object>(), It.IsAny<JsonSerializerOptions>()))
			.Returns(expectedSerialized);

		var serviceCollection = new ServiceCollection();
		serviceCollection.AddSingleton(mockSerializer.Object);
		var serviceProvider = serviceCollection.BuildServiceProvider();
		httpContext.RequestServices = serviceProvider;

		await result.ExecuteAsync(httpContext);

		Assert.Equal(200, httpContext.Response.StatusCode);

		httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
		using var reader = new StreamReader(httpContext.Response.Body);
		var responseBody = await reader.ReadToEndAsync();

		Assert.Equal(200, httpContext.Response.StatusCode);
		Assert.NotEmpty(responseBody);
	}
}
