---
sidebar_position: 3
title: Testing
---

# Testing

This page covers comprehensive testing strategies for AsyncEndpoints applications, including unit testing, integration testing, and end-to-end testing approaches.

## Overview

Testing AsyncEndpoints applications requires a multi-layered approach covering handlers, configuration, and the overall async processing workflow. The asynchronous and distributed nature of the system adds complexity but also enables powerful testing patterns.

## Unit Testing Strategies

### Handler Unit Testing

Handlers should be designed for testability with dependency injection:

```csharp
// Well-designed handler for testing
public class ProcessDataHandler(
    ILogger<ProcessDataHandler> logger,
    IDataProcessor dataProcessor,
    IFileService fileService) 
    : IAsyncEndpointRequestHandler<DataRequest, ProcessResult>
{
    public async Task<MethodResult<ProcessResult>> HandleAsync(AsyncContext<DataRequest> context, CancellationToken token)
    {
        var request = context.Request;
        
        try
        {
            // Use injected dependencies
            var processedData = await dataProcessor.ProcessAsync(request.Data, token);
            var result = new ProcessResult
            {
                ProcessedData = processedData,
                ProcessedAt = DateTime.UtcNow,
                CharacterCount = processedData.Length
            };
            
            return MethodResult<ProcessResult>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing request: {Data}", request.Data);
            return MethodResult<ProcessResult>.Failure(ex);
        }
    }
}
```

### Handler Test Example

```csharp
public class ProcessDataHandlerTests
{
    private readonly Mock<ILogger<ProcessDataHandler>> _mockLogger;
    private readonly Mock<IDataProcessor> _mockDataProcessor;
    private readonly Mock<IFileService> _mockFileService;
    private readonly ProcessDataHandler _handler;
    
    public ProcessDataHandlerTests()
    {
        _mockLogger = new Mock<ILogger<ProcessDataHandler>>();
        _mockDataProcessor = new Mock<IDataProcessor>();
        _mockFileService = new Mock<IFileService>();
        
        _handler = new ProcessDataHandler(_mockLogger.Object, _mockDataProcessor.Object, _mockFileService.Object);
    }
    
    [Fact]
    public async Task HandleAsync_WhenProcessingSuccessful_ReturnsSuccessResult()
    {
        // Arrange
        var requestData = "test data";
        var expectedProcessedData = "PROCESSED: TEST DATA";
        
        _mockDataProcessor
            .Setup(dp => dp.ProcessAsync(requestData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProcessedData);
        
        var context = CreateTestContext(new DataRequest { Data = requestData });
        
        // Act
        var result = await _handler.HandleAsync(context, CancellationToken.None);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(expectedProcessedData, result.Data.ProcessedData);
        Assert.Equal(expectedProcessedData.Length, result.Data.CharacterCount);
        
        _mockDataProcessor.Verify(dp => dp.ProcessAsync(requestData, It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task HandleAsync_WhenProcessingThrowsException_ReturnsFailureResult()
    {
        // Arrange
        var requestData = "test data";
        var expectedException = new InvalidOperationException("Processing failed");
        
        _mockDataProcessor
            .Setup(dp => dp.ProcessAsync(requestData, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);
        
        var context = CreateTestContext(new DataRequest { Data = requestData });
        
        // Act
        var result = await _handler.HandleAsync(context, CancellationToken.None);
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(expectedException.GetType().Name, result.Error.Exception?.Type);
        
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error processing request")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    
    private AsyncContext<DataRequest> CreateTestContext(DataRequest request)
    {
        return new AsyncContext<DataRequest>(
            request,
            new Dictionary<string, List<string?>>() { { "Authorization", new List<string?> { "Bearer token123" } } },
            new Dictionary<string, object?>() { { "userId", "123" } },
            new List<KeyValuePair<string, List<string?>>>()
            {
                new KeyValuePair<string, List<string?>>("format", new List<string?> { "json" })
            });
    }
}
```

### Testing with HTTP Context Preservation

```csharp
[Fact]
public async Task HandleAsync_WhenHttpContextPreserved_UsesContextCorrectly()
{
    // Arrange
    var headers = new Dictionary<string, List<string?>> 
    { 
        { "X-User-Id", new List<string?> { "test-user" } },
        { "Authorization", new List<string?> { "Bearer token" } }
    };
    
    var routeParams = new Dictionary<string, object?> 
    { 
        { "tenantId", "test-tenant" },
        { "action", "process" }
    };
    
    var queryParams = new List<KeyValuePair<string, List<string?>>>
    {
        new KeyValuePair<string, List<string?>>("priority", new List<string?> { "high" })
    };
    
    var context = new AsyncContext<DataRequest>(
        new DataRequest { Data = "test" },
        headers,
        routeParams,
        queryParams);
    
    // Act
    var result = await _handler.HandleAsync(context, CancellationToken.None);
    
    // Additional assertions can verify that context data was used appropriately
    Assert.True(result.IsSuccess);
}
```

## Mocking Dependencies

### Mocking Job Store

```csharp
public class JobStoreTests
{
    [Fact]
    public async Task GetJobById_WhenJobExists_ReturnsJob()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var expectedJob = new Job
        {
            Id = jobId,
            Name = "TestJob",
            Status = JobStatus.Queued,
            Payload = "{}"
        };
        
        var mockJobStore = new Mock<IJobStore>();
        mockJobStore
            .Setup(store => store.GetJobById(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MethodResult<Job>.Success(expectedJob));
        
        // Act
        var result = await mockJobStore.Object.GetJobById(jobId, CancellationToken.None);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(jobId, result.Data?.Id);
    }
    
    [Fact]
    public async Task CreateJob_WhenJobCreatedSuccessfully_ReturnsSuccess()
    {
        // Arrange
        var job = new Job { Id = Guid.NewGuid(), Name = "TestJob", Status = JobStatus.Queued };
        var mockJobStore = new Mock<IJobStore>();
        mockJobStore
            .Setup(store => store.CreateJob(job, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MethodResult.Success());
        
        // Act
        var result = await mockJobStore.Object.CreateJob(job, CancellationToken.None);
        
        // Assert
        Assert.True(result.IsSuccess);
    }
}
```

### Mocking HTTP Context

```csharp
public static class HttpContextMockHelper
{
    public static HttpContext CreateMockHttpContext()
    {
        var context = new DefaultHttpContext();
        
        // Add headers
        context.Request.Headers["Authorization"] = "Bearer test-token";
        context.Request.Headers["X-User-Id"] = "test-user";
        context.Request.Headers["Content-Type"] = "application/json";
        
        // Add route values (requires setting up routing)
        var routeData = new RouteData();
        routeData.Values.Add("userId", "123");
        routeData.Values.Add("action", "process");
        context.Features.Set<IRoutingFeature>(new RoutingFeature
        {
            RouteData = routeData
        });
        
        return context;
    }
    
    public static HttpContext CreateMockHttpContextWithBody<T>(T requestBody)
    {
        var context = CreateMockHttpContext();
        
        var json = JsonSerializer.Serialize(requestBody);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        context.Request.Body = stream;
        context.Request.ContentLength = stream.Length;
        context.Request.ContentType = "application/json";
        
        return context;
    }
}
```

## Integration Testing

### Testing Job Lifecycle

```csharp
public class JobIntegrationTests : IAsyncLifetime
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IJobStore _jobStore;
    private readonly IJobManager _jobManager;
    
    public JobIntegrationTests()
    {
        // Create service provider with test configuration
        var services = new ServiceCollection();
        
        services.AddSingleton<ILogger<JobStoreTests>>(new Mock<ILogger<JobStoreTests>>().Object);
        services.AddSingleton<IDateTimeProvider, MockDateTimeProvider>();
        
        // Use in-memory store for testing
        services.AddSingleton<IJobStore, InMemoryJobStore>();
        services.AddScoped<IJobManager, JobManager>();
        
        _serviceProvider = services.BuildServiceProvider();
        _jobStore = _serviceProvider.GetRequiredService<IJobStore>();
        _jobManager = _serviceProvider.GetRequiredService<IJobManager>();
    }
    
    [Fact]
    public async Task CompleteJobLifecycle_CreatesProcessesAndCompletesJob()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var jobName = "TestJob";
        var payload = JsonSerializer.Serialize(new { testData = "value" });
        
        // Create HTTP context
        var httpContext = HttpContextMockHelper.CreateMockHttpContext();
        
        // Act - Create job
        var createResult = await _jobManager.SubmitJob(jobName, payload, httpContext, CancellationToken.None);
        
        // Assert - Job created successfully
        Assert.True(createResult.IsSuccess);
        Assert.NotNull(createResult.Data);
        Assert.Equal(jobId, createResult.Data.Id);
        Assert.Equal(JobStatus.Queued, createResult.Data.Status);
        
        // Act - Retrieve job
        var getResult = await _jobStore.GetJobById(jobId, CancellationToken.None);
        
        // Assert - Job retrieved with correct data
        Assert.True(getResult.IsSuccess);
        Assert.NotNull(getResult.Data);
        Assert.Equal(jobName, getResult.Data.Name);
        Assert.Equal("value", JsonSerializer.Deserialize<Dictionary<string, object>>(getResult.Data.Payload)["testData"]);
    }
    
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;
}
```

### Testing Handler Execution

```csharp
public class HandlerExecutionTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<TestHandler> _mockTestHandler;
    private readonly HandlerExecutionService _handlerExecutionService;
    
    public HandlerExecutionTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockTestHandler = new Mock<TestHandler>();
        
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IAsyncEndpointRequestHandler<DataRequest, ProcessResult>)))
            .Returns(_mockTestHandler.Object);
        
        _handlerExecutionService = new HandlerExecutionService(_mockServiceProvider.Object);
    }
    
    [Fact]
    public async Task ExecuteHandler_WhenHandlerSuccessful_ReturnsSuccess()
    {
        // Arrange
        var job = CreateTestJob();
        var expectedResult = MethodResult<ProcessResult>.Success(new ProcessResult());
        
        _mockTestHandler
            .Setup(h => h.HandleAsync(It.IsAny<AsyncContext<DataRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);
        
        // Act
        var result = await _handlerExecutionService.ExecuteHandler<DataRequest, ProcessResult>(job, CancellationToken.None);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedResult.Data, result.Data);
    }
}
```

## Testing with TestServer

### Web Application Integration Tests

```csharp
public class WebAppIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    
    public WebAppIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace real services with test doubles
                services.AddSingleton<IJobStore, InMemoryJobStore>();
                // Register actual handlers or mocks as needed for testing
                // services.AddTransient<IAsyncEndpointRequestHandler<YourRequestType, YourResponseType>, YourHandlerImplementation>();
            });
        });
    }
    
    [Fact]
    public async Task PostAsyncEndpoint_SubmitJob_ReturnsAccepted()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new DataRequest { Data = "test data", ProcessingPriority = 1 };
        
        // Act
        var response = await client.PostAsJsonAsync("/api/process-data", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        
        var jobResponse = await response.Content.ReadFromJsonAsync<Job>();
        Assert.NotNull(jobResponse);
        Assert.Equal(JobStatus.Queued, jobResponse.Status);
    }
    
    [Fact]
    public async Task GetJobStatus_CreatedJob_ReturnsJobStatus()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new DataRequest { Data = "test data", ProcessingPriority = 1 };
        
        // Create and get job ID
        var createResponse = await client.PostAsJsonAsync("/api/process-data", request);
        var createdJob = await createResponse.Content.ReadFromJsonAsync<Job>();
        var jobId = createdJob.Id;
        
        // Act
        var statusResponse = await client.GetAsync($"/jobs/{jobId}");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        
        var jobStatus = await statusResponse.Content.ReadFromJsonAsync<Job>();
        Assert.NotNull(jobStatus);
        Assert.Equal(jobId, jobStatus.Id);
    }
}
```

### Background Service Testing

```csharp
public class BackgroundServiceTests
{
    [Fact]
    public async Task BackgroundService_WhenStarted_ProcessesJobs()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Add test services
        services.AddLogging();
        services.AddSingleton<IJobStore, InMemoryJobStore>();
        
        // Create mocks for job processing services
        var mockJobProducer = new Mock<IJobProducerService>();
        var mockJobConsumer = new Mock<IJobConsumerService>();
        var mockJobClaiming = new Mock<IJobClaimingService>();
        var mockHandlerExecution = new Mock<IHandlerExecutionService>();
        var mockDelayCalculator = new Mock<IDelayCalculatorService>();
        
        services.AddSingleton(mockJobProducer.Object);
        services.AddSingleton(mockJobConsumer.Object);
        services.AddSingleton(mockJobClaiming.Object);
        services.AddSingleton(mockHandlerExecution.Object);
        services.AddSingleton(mockDelayCalculator.Object);
        
        // Create and register a mock IDateTimeProvider
        var mockDateTimeProvider = new Mock<IDateTimeProvider>();
        mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(DateTimeOffset.UtcNow);
        mockDateTimeProvider.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);
        services.AddSingleton(mockDateTimeProvider.Object);
        
        // Add the actual background service implementation
        services.AddSingleton<IHostedService, AsyncEndpointsBackgroundService>();
        
        using var serviceProvider = services.BuildServiceProvider();
        
        // Add some test jobs
        var jobStore = serviceProvider.GetRequiredService<IJobStore>();
        var testJob = new Job 
        { 
            Id = Guid.NewGuid(), 
            Name = "TestJob", 
            Status = JobStatus.Queued,
            Payload = "{}"
        };
        await jobStore.CreateJob(testJob, CancellationToken.None);
        
        // Act
        var hostedService = serviceProvider.GetRequiredService<IHostedService>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        
        // Start the service
        await hostedService.StartAsync(cts.Token);
        
        // Wait for processing to occur
        await Task.Delay(1000, cts.Token);
        
        // Stop the service
        await hostedService.StopAsync(cts.Token);
        
        // Assert
        var processedJobs = await GetProcessedJobs(jobStore);
        Assert.Contains(testJob.Id, processedJobs);
    }
}
```

## End-to-End Testing

### Complete Workflow Tests

```csharp
public class EndToEndTests
{
    [Fact]
    public async Task ProcessingWorkflow_CompleteScenario_CompletesSuccessfully()
    {
        // This test simulates a complete async processing scenario
        
        // 1. Submit job via HTTP
        var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        
        var requestData = new DataRequest { Data = "Hello AsyncEndpoints!", ProcessingPriority = 2 };
        var submitResponse = await client.PostAsJsonAsync("/api/process-data", requestData);
        
        Assert.Equal(HttpStatusCode.Accepted, submitResponse.StatusCode);
        var acceptedJob = await submitResponse.Content.ReadFromJsonAsync<Job>();
        Assert.NotNull(acceptedJob);
        var jobId = acceptedJob.Id;
        
        // 2. Wait for background processing (with timeout)
        var timeout = TimeSpan.FromSeconds(30);
        var startTime = DateTime.UtcNow;
        
        Job jobStatus;
        do
        {
            await Task.Delay(1000); // Poll every second
            
            var statusResponse = await client.GetAsync($"/jobs/{jobId}");
            jobStatus = await statusResponse.Content.ReadFromJsonAsync<Job>();
            
            Assert.NotNull(jobStatus);
            
            if (DateTime.UtcNow - startTime > timeout)
            {
                Assert.Fail($"Job did not complete within timeout period: {timeout}");
            }
        } while (jobStatus.Status != JobStatus.Completed && jobStatus.Status != JobStatus.Failed);
        
        // 3. Verify final state
        Assert.Equal(JobStatus.Completed, jobStatus.Status);
        Assert.NotNull(jobStatus.Result);
        Assert.NotNull(jobStatus.CompletedAt);
    }
}
```

### Testing with Real Background Processing

```csharp
public class RealProcessingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    
    public RealProcessingTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }
    
    [Fact]
    public async Task RealBackgroundProcessing_CompletesProcessing()
    {
        // Configure factory to enable background services
        var factoryWithServices = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                // Ensure background services are registered
                services.AddAsyncEndpointsWorker();
                
                // Optionally override with test-friendly settings
                services.Configure<AsyncEndpointsConfigurations>(config =>
                {
                    config.WorkerConfigurations.PollingIntervalMs = 100; // Faster polling for tests
                    config.JobManagerConfiguration.JobPollingIntervalMs = 100;
                });
            });
        });
        
        using var client = factoryWithServices.CreateClient();
        
        // Submit a test job
        var request = new DataRequest { Data = "integration test", ProcessingPriority = 1 };
        var submitResponse = await client.PostAsJsonAsync("/api/process-data", request);
        
        Assert.Equal(HttpStatusCode.Accepted, submitResponse.StatusCode);
        
        var job = await submitResponse.Content.ReadFromJsonAsync<Job>();
        Assert.NotNull(job);
        
        // Wait and check job status
        var completedJob = await WaitJobCompletion(client, job.Id, TimeSpan.FromSeconds(10));
        
        Assert.NotNull(completedJob);
        Assert.True(completedJob.Status == JobStatus.Completed || 
                   completedJob.Status == JobStatus.Failed);
    }
    
    private async Task<Job?> WaitJobCompletion(HttpClient client, Guid jobId, TimeSpan timeout)
    {
        var startTime = DateTime.UtcNow;
        Job? job = null;
        
        do
        {
            await Task.Delay(500); // Wait 500ms between checks
            
            var response = await client.GetAsync($"/jobs/{jobId}");
            if (response.IsSuccessStatusCode)
            {
                job = await response.Content.ReadFromJsonAsync<Job>();
            }
            
            if (DateTime.UtcNow - startTime > timeout)
            {
                return job; // Return latest state even if not completed
            }
        } while (job?.Status == JobStatus.Queued || job?.Status == JobStatus.InProgress);
        
        return job;
    }
}
```

## Testing Best Practices

### Use Test Helpers

Create helper classes to reduce test boilerplate:

```csharp
public static class TestHelpers
{
    public static T CreateTestInstance<T>() where T : new()
    {
        return new T();
    }
    
    public static AsyncContext<TRequest> CreateContext<TRequest>(TRequest request)
    {
        return new AsyncContext<TRequest>(
            request,
            new Dictionary<string, List<string?>>(),
            new Dictionary<string, object?>(),
            new List<KeyValuePair<string, List<string?>>>());
    }
    
    public static MockServiceProvider CreateMockServiceProvider()
    {
        var mockServiceProvider = new Mock<IServiceProvider>();
        var serviceScope = new Mock<IServiceScope>();
        
        serviceScope.Setup(scope => scope.ServiceProvider).Returns(mockServiceProvider.Object);
        
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(factory => factory.CreateScope()).Returns(serviceScope.Object);
        
        mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IServiceScopeFactory)))
            .Returns(scopeFactory.Object);
        
        return mockServiceProvider;
    }
}
```

### Test Configuration

```csharp
public class TestConfiguration
{
    public static IServiceCollection ConfigureTestServices(IServiceCollection services, Mock<IDateTimeProvider>? mockDateTimeProvider = null)
    {
        // Register test doubles
        services.AddSingleton<IJobStore, InMemoryJobStore>();
        
        // Create and register a mock IDateTimeProvider
        mockDateTimeProvider ??= new Mock<IDateTimeProvider>();
        mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(DateTimeOffset.UtcNow);
        mockDateTimeProvider.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);
        services.AddSingleton(mockDateTimeProvider.Object);
        
        // Note: ISerializer would need to be mocked separately in actual tests
        // var mockSerializer = new Mock<ISerializer>();
        // services.AddSingleton(mockSerializer.Object);

        // Configure for testing
        services.Configure<AsyncEndpointsConfigurations>(config =>
        {
            config.WorkerConfigurations.MaximumConcurrency = 1;
            config.WorkerConfigurations.MaximumQueueSize = 10;
            config.WorkerConfigurations.PollingIntervalMs = 100;
            config.JobManagerConfiguration.DefaultMaxRetries = 0; // No retries in tests
        });
        
        return services;
    }
}
```

### Async Testing Patterns

```csharp
public static class AsyncTestExtensions
{
    public static async Task<T> ShouldCompleteWithin<T>(this Task<T> task, TimeSpan timeout)
    {
        var completedTask = await Task.WhenAny(task, Task.Delay(timeout));
        if (completedTask == task)
        {
            return await task;
        }
        
        throw new TimeoutException($"Task did not complete within {timeout}");
    }
    
    public static async Task ShouldCompleteWithin(this Task task, TimeSpan timeout)
    {
        var completedTask = await Task.WhenAny(task, Task.Delay(timeout));
        if (completedTask == task)
        {
            await task;
            return;
        }
        
        throw new TimeoutException($"Task did not complete within {timeout}");
    }
}
```

## Troubleshooting Common Testing Issues

### Mocking Complex Dependencies

When testing handlers with complex dependencies:

```csharp
// Use dependency factories for complex setups
public class ComplexDependencyFactory
{
    public static Mock<ComplexDependency> CreateWithDefaults()
    {
        var mock = new Mock<ComplexDependency>();
        
        mock.Setup(d => d.GetConfiguration())
            .Returns(new Configuration 
            { 
                Timeout = TimeSpan.FromSeconds(30),
                MaxRetries = 3 
            });
        
        return mock;
    }
}
```

### Handling Async Race Conditions

For tests that involve timing and concurrency:

```csharp
[Fact]
public async Task RaceConditionTest()
{
    // Use synchronization primitives to control timing
    var startSignal = new ManualResetEvent(false);
    var completionSignal = new CountdownEvent(5); // Wait for 5 operations
    
    // Start multiple async operations
    var tasks = Enumerable.Range(0, 5)
        .Select(i => StartOperationAsync(startSignal, completionSignal))
        .ToArray();
    
    // Signal all operations to start simultaneously
    startSignal.Set();
    
    // Wait for all to complete
    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
    var allCompletedTask = Task.Run(() => completionSignal.Wait());
    
    var completedTask = await Task.WhenAny(allCompletedTask, timeoutTask);
    
    if (completedTask == timeoutTask)
    {
        Assert.Fail("Operations did not complete within timeout");
    }
    
    // Verify results
    var results = await Task.WhenAll(tasks);
    Assert.All(results, result => Assert.NotNull(result));
}
```

Testing AsyncEndpoints applications requires careful attention to asynchronous behavior, timing, and the distributed nature of the processing system. The key is to use appropriate test doubles, configure services for testability, and validate both individual components and the complete workflow.