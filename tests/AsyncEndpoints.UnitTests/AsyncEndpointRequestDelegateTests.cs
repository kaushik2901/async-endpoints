using AsyncEndpoints.Contracts;
using AsyncEndpoints.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AsyncEndpoints.UnitTests;

public class AsyncEndpointRequestDelegateTests
{
    private readonly Mock<ILogger<AsyncEndpointRequestDelegate>> _mockLogger;
    private readonly Mock<IJobStore> _mockJobStore;
    private readonly IOptions<JsonOptions> _jsonOptions;

    public AsyncEndpointRequestDelegateTests()
    {
        _mockLogger = new Mock<ILogger<AsyncEndpointRequestDelegate>>();
        _mockJobStore = new Mock<IJobStore>();
        _jsonOptions = Options.Create(new JsonOptions());
    }

    [Fact]
    public async Task Constructor_CreatesInstance()
    {
        // Act
        var requestDelegate = new AsyncEndpointRequestDelegate(_mockLogger.Object, _mockJobStore.Object, _jsonOptions);

        // Assert
        Assert.NotNull(requestDelegate);
    }

    [Fact]
    public async Task HandleAsync_WithCustomHandler_ReturnsCustomHandlerResult()
    {
        // Arrange
        var requestDelegate = new AsyncEndpointRequestDelegate(_mockLogger.Object, _mockJobStore.Object, _jsonOptions);
        var httpContext = CreateHttpContext();
        var request = new TestRequest { Value = "test" };
        var expectedResponse = Results.Ok("Custom Response");
        Func<HttpContext, TestRequest, CancellationToken, Task<IResult?>> customHandler =
            (ctx, req, token) => Task.FromResult<IResult?>(expectedResponse);

        // Act
        var result = await requestDelegate.HandleAsync("test-job", httpContext, request, customHandler);

        // Assert
        Assert.Same(expectedResponse, result);
        _mockJobStore.Verify(store => store.Get(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithCustomHandler_CanBeCalledWithoutError()
    {
        // Arrange
        var requestDelegate = new AsyncEndpointRequestDelegate(_mockLogger.Object, _mockJobStore.Object, _jsonOptions);
        var httpContext = CreateHttpContext();
        var request = new TestRequest { Value = "test" };
        var expectedResponse = Results.Ok("Custom Response");
        Func<HttpContext, TestRequest, CancellationToken, Task<IResult?>> customHandler =
            (ctx, req, token) => Task.FromResult<IResult?>(expectedResponse);

        // Act
        var result = await requestDelegate.HandleAsync("test-job", httpContext, request, customHandler);

        // Assert - The main thing is that it doesn't throw an exception when custom handler is provided
        Assert.NotNull(result);
    }

    private HttpContext CreateHttpContext()
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