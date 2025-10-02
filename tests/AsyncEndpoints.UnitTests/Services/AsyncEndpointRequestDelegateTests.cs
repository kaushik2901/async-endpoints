using AsyncEndpoints.Contracts;
using AsyncEndpoints.Serialization;
using AsyncEndpoints.Services;
using AsyncEndpoints.UnitTests.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.UnitTests.Services;

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