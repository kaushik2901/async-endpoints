using AsyncEndpoints.AspNetCore.Configuration;
using AsyncEndpoints.AspNetCore.Handlers;
using AsyncEndpoints.AspNetCore.UnitTests.TestSupport;
using AsyncEndpoints.Core.Infrastructure.Serialization;
using AsyncEndpoints.Core.Legacy.JobProcessing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.AspNetCore.UnitTests.Handlers;

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
		var configurations = new AsyncEndpointsResponseConfigurations();

		var requestDelegate = new AsyncEndpointRequestDelegate(_mockLogger.Object, _mockJobManager.Object, _mockSerializer.Object, configurations);

		Assert.NotNull(requestDelegate);
	}

	[Fact]
	public async Task HandleAsync_WithCustomHandler_CanBeCalledWithoutError()
	{
		var configurations = new AsyncEndpointsResponseConfigurations();
		var requestDelegate = new AsyncEndpointRequestDelegate(_mockLogger.Object, _mockJobManager.Object, _mockSerializer.Object, configurations);
		var httpContext = CreateHttpContext;
		var request = new TestRequest { Value = "test" };
		var expectedResponse = Results.Ok("Custom Response");
		Func<HttpContext, TestRequest, CancellationToken, Task<IResult?>> customHandler =
			(ctx, req, token) => Task.FromResult<IResult?>(expectedResponse);

		var result = await requestDelegate.HandleAsync("test-job", httpContext, request, customHandler);

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

			var serviceProvider = new ServiceCollection().BuildServiceProvider();
			context.RequestServices = serviceProvider;

			return context;
		}
	}
}
