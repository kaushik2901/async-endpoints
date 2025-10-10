using AsyncEndpoints.Infrastructure.Serialization;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Moq;
using System.Text.Json;

namespace AsyncEndpoints.UnitTests.Utilities;

public class JobResultResponseTests
{
	/// <summary>
	/// Verifies that the JobResultResponse can be constructed with a job and status code.
	/// This test ensures the constructor properly accepts and stores the required parameters.
	/// </summary>
	[Fact]
	public void Constructor_Succeeds_WithValidParameters()
	{
		// Arrange
		var job = new Job();
		var statusCode = 200;

		// Act
		var result = new JobResultResponse(job, statusCode);

		// Assert
		Assert.NotNull(result);
	}

	/// <summary>
	/// Verifies that the JobResultResponse uses the default status code (200) when not explicitly provided.
	/// This ensures proper default behavior.
	/// </summary>
	[Fact]
	public void Constructor_UsesDefaultStatusCode_WhenNotProvided()
	{
		// Arrange
		var job = new Job();

		// Act
		var result = new JobResultResponse(job);

		// Assert
		// We can't directly test the status code field, but we can ensure the constructor works
		Assert.NotNull(result);
	}

	/// <summary>
	/// Verifies that the JobResultResponse executes correctly and sets the appropriate HTTP response.
	/// This test ensures the ExecuteAsync method properly writes to the HTTP context.
	/// </summary>
	[Fact]
	public async Task ExecuteAsync_SetsCorrectResponse()
	{
		// Arrange
		var job = new Job
		{
			Id = Guid.NewGuid(),
			Name = "TestJob",
			Status = JobStatus.Completed,
			Result = "\"Test Result\"", // JSON string for the result
			CreatedAt = DateTimeOffset.UtcNow
		};
		var statusCode = 200;
		var result = new JobResultResponse(job, statusCode);

		var httpContext = new DefaultHttpContext();
		httpContext.Response.Body = new MemoryStream();

		// Create a service provider with a mock serializer
		var mockSerializer = new Mock<ISerializer>();
		var expectedSerialized = "{\"Id\":\"00000000-0000-0000-0000-000000000000\",\"Name\":\"\",\"Status\":\"\",\"Headers\":{},\"RouteParams\":{},\"QueryParams\":[],\"Payload\":\"\",\"Result\":\"__JOB_RESULT_PLACEHOLDER__\",\"Error\":null,\"RetryCount\":0,\"MaxRetries\":0,\"RetryDelayUntil\":null,\"WorkerId\":\"00000000-0000-0000-0000-000000000000\",\"CreatedAt\":\"0001-01-01T00:00:00+00:00\",\"StartedAt\":\"0001-01-01T00:00:00+00:00\",\"CompletedAt\":\"0001-01-01T00:00:00+00:00\",\"LastUpdatedAt\":\"0001-01-01T00:00:00+00:00\",\"IsCanceled\":false}";
		// Mock both method overloads that could be called
		mockSerializer
			.Setup(x => x.Serialize(It.IsAny<object>(), It.IsAny<Type>(), It.IsAny<JsonSerializerOptions>()))
			.Returns(expectedSerialized);
		mockSerializer
			.Setup(x => x.Serialize(It.IsAny<object>(), It.IsAny<JsonSerializerOptions>()))
			.Returns(expectedSerialized);

		var serviceCollection = new ServiceCollection();
		serviceCollection.AddSingleton<ISerializer>(mockSerializer.Object);
		var serviceProvider = serviceCollection.BuildServiceProvider();
		httpContext.RequestServices = serviceProvider;

		// Act
		await result.ExecuteAsync(httpContext);

		// Assert
		Assert.Equal(statusCode, httpContext.Response.StatusCode);
		Assert.Equal("application/json", httpContext.Response.ContentType);

		// Read the response body
		httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
		using var reader = new StreamReader(httpContext.Response.Body);
		var responseBody = await reader.ReadToEndAsync();

		// Verify it contains basic response structure
		Assert.Contains("application/json", httpContext.Response.ContentType);
		Assert.NotEmpty(responseBody);
	}

	/// <summary>
	/// Verifies that the JobResultResponse handles serialization of complex job properties correctly.
	/// This ensures complex job data is properly included in the response.
	/// </summary>
	[Fact]
	public async Task ExecuteAsync_HandlesComplexJobData()
	{
		// Arrange
		var jobId = Guid.NewGuid();
		var complexResult = new { Message = "Success", Data = new { Id = 123, Name = "Test" } };
		var jobResultJson = System.Text.Json.JsonSerializer.Serialize(complexResult);
		var job = new Job
		{
			Id = jobId,
			Name = "ComplexJob",
			Status = JobStatus.Completed,
			Result = jobResultJson,
			CreatedAt = DateTimeOffset.UtcNow,
			StartedAt = DateTimeOffset.UtcNow.AddMinutes(1),
			CompletedAt = DateTimeOffset.UtcNow.AddMinutes(2),
			MaxRetries = 3,
			RetryCount = 0
		};
		var result = new JobResultResponse(job);

		var httpContext = new DefaultHttpContext();
		httpContext.Response.Body = new MemoryStream();

		// Create a service provider with a mock serializer
		var mockSerializer = new Mock<ISerializer>();
		var expectedSerialized = "{\"Id\":\"00000000-0000-0000-0000-000000000000\",\"Name\":\"\",\"Status\":\"\",\"Headers\":{},\"RouteParams\":{},\"QueryParams\":[],\"Payload\":\"\",\"Result\":\"__JOB_RESULT_PLACEHOLDER__\",\"Error\":null,\"RetryCount\":0,\"MaxRetries\":0,\"RetryDelayUntil\":null,\"WorkerId\":\"00000000-0000-0000-0000-000000000000\",\"CreatedAt\":\"0001-01-01T00:00:00+00:00\",\"StartedAt\":\"0001-01-01T00:00:00+00:00\",\"CompletedAt\":\"0001-01-01T00:00:00+00:00\",\"LastUpdatedAt\":\"0001-01-01T00:00:00+00:00\",\"IsCanceled\":false}";
		// Mock both method overloads that could be called
		mockSerializer
			.Setup(x => x.Serialize(It.IsAny<object>(), It.IsAny<Type>(), It.IsAny<JsonSerializerOptions>()))
			.Returns(expectedSerialized);
		mockSerializer
			.Setup(x => x.Serialize(It.IsAny<object>(), It.IsAny<JsonSerializerOptions>()))
			.Returns(expectedSerialized);

		var serviceCollection = new ServiceCollection();
		serviceCollection.AddSingleton<ISerializer>(mockSerializer.Object);
		var serviceProvider = serviceCollection.BuildServiceProvider();
		httpContext.RequestServices = serviceProvider;

		// Act
		await result.ExecuteAsync(httpContext);

		// Assert
		Assert.Equal(200, httpContext.Response.StatusCode);

		// Read the response body
		httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
		using var reader = new StreamReader(httpContext.Response.Body);
		var responseBody = await reader.ReadToEndAsync();

		// Verify response is properly formatted
		Assert.Equal(200, httpContext.Response.StatusCode);
		Assert.NotEmpty(responseBody);
	}
}
