using AsyncEndpoints.Configuration;
using AsyncEndpoints.Handlers;
using AsyncEndpoints.Infrastructure.Serialization;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.UnitTests.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.UnitTests.Handlers;

public class AsyncEndpointRequestDelegateTests
{
	private readonly Mock<ILogger<AsyncEndpointRequestDelegate>> _mockLogger;
	private readonly Mock<IJobManager> _mockJobManager;
	private readonly Mock<ISerializer> _mockSerializer;

	public AsyncEndpointRequestDelegateTests()
	{
		_mockLogger = new Mock<ILogger<AsyncEndpointRequestDelegate>>();
		_mockJobManager = new Mock<IJobManager>();
		_mockSerializer = new Mock<ISerializer>();
	}

	/// <summary>
	/// Verifies that the AsyncEndpointRequestDelegate can be constructed with valid dependencies without throwing an exception.
	/// This test ensures the constructor properly accepts and stores all required dependencies.
	/// </summary>
	[Fact]
	public void Constructor_CreatesInstance()
	{
		// Arrange
		var configurations = new AsyncEndpointsConfigurations();

		// Act
		var requestDelegate = new AsyncEndpointRequestDelegate(_mockLogger.Object, _mockJobManager.Object, _mockSerializer.Object, configurations);

		// Assert
		Assert.NotNull(requestDelegate);
	}

	/// <summary>
	/// Verifies that the AsyncEndpointRequestDelegate can handle requests with a custom handler without throwing exceptions.
	/// This test ensures the request delegate properly supports custom handler execution.
	/// </summary>
	[Fact]
	public async Task HandleAsync_WithCustomHandler_CanBeCalledWithoutError()
	{
		// Arrange
		var configurations = new AsyncEndpointsConfigurations();
		var requestDelegate = new AsyncEndpointRequestDelegate(_mockLogger.Object, _mockJobManager.Object, _mockSerializer.Object, configurations);
		var httpContext = CreateHttpContext;
		var request = new TestRequest { Value = "test" };
		var expectedResponse = Results.Ok("Custom Response");
		Func<HttpContext, TestRequest, CancellationToken, Task<IResult?>> customHandler =
			(ctx, req, token) => Task.FromResult<IResult?>(expectedResponse);

		// Act
		var result = await requestDelegate.HandleAsync("test-job", httpContext, request, customHandler);

		// Assert - The main thing is that it doesn't throw an exception when custom handler is provided
		Assert.NotNull(result);
	}

	private static HttpContext CreateHttpContext
	{
		get
		{
			var context = new DefaultHttpContext();
			context.Request.Method = "POST";
			context.Request.Path = "/test";
			context.Request.ContentLength = 0;

			// Set up a basic service provider
			var serviceProvider = new ServiceCollection().BuildServiceProvider();
			context.RequestServices = serviceProvider;

			return context;
		}
	}
}
