---
sidebar_position: 13
---

# Testing

## Overview

Testing AsyncEndpoints applications requires understanding both the synchronous API layer and the asynchronous background processing. This guide covers testing strategies for handlers, storage, and the overall async workflow.

## Testing Handlers

### Unit Testing Handlers

Handler classes can be unit tested like any other class:

```csharp
using AsyncEndpoints.Handlers;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.Logging;
using Moq;

public class MyHandlerTests
{
    private readonly Mock<ILogger<MyHandler>> _loggerMock;
    private readonly MyHandler _handler;

    public MyHandlerTests()
    {
        _loggerMock = new Mock<ILogger<MyHandler>>();
        _handler = new MyHandler(_loggerMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new MyRequest { Data = "test" };
        var context = new AsyncContext<MyRequest>(request, new Dictionary<string, List<string?>>(), 
            new Dictionary<string, object?>(), new List<KeyValuePair<string, List<string?>>>());
        
        // Act
        var result = await _handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidRequest_ReturnsFailure()
    {
        // Arrange
        var request = new MyRequest { Data = "" }; // Invalid request
        var context = new AsyncContext<MyRequest>(request, new Dictionary<string, List<string?>>(), 
            new Dictionary<string, object?>(), new List<KeyValuePair<string, List<string?>>>());

        // Act
        var result = await _handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }
}
```

### Testing with Dependencies

When your handler has dependencies, mock them appropriately:

```csharp
public class ServiceUsingHandlerTests
{
    private readonly Mock<IMyService> _myServiceMock;
    private readonly Mock<ILogger<ServiceUsingHandler>> _loggerMock;
    private readonly ServiceUsingHandler _handler;

    public ServiceUsingHandlerTests()
    {
        _myServiceMock = new Mock<IMyService>();
        _loggerMock = new Mock<ILogger<ServiceUsingHandler>>();
        
        // Inject mocked dependencies
        _handler = new ServiceUsingHandler(_myServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ServiceReturnsSuccess_HandlesSuccessfully()
    {
        // Arrange
        var expected = new MyResult { Success = true };
        _myServiceMock.Setup(s => s.ProcessAsync(It.IsAny<MyRequest>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(expected);
        
        var request = new MyRequest { Data = "test" };
        var context = new AsyncContext<MyRequest>(request, new Dictionary<string, List<string?>>(), 
            new Dictionary<string, object?>(), new List<KeyValuePair<string, List<string?>>>());

        // Act
        var result = await _handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Same(expected, result.Data);
        _myServiceMock.Verify(s => s.ProcessAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

## Testing HTTP Context Access

Test that your handlers properly use HTTP context information:

```csharp
public class ContextAwareHandlerTests
{
    [Fact]
    public async Task HandleAsync_HasHeaders_ContextContainsHeaders()
    {
        // Arrange
        var request = new MyRequest { Data = "test" };
        var headers = new Dictionary<string, List<string?>> 
        { 
            { "Authorization", new List<string?> { "Bearer token123" } },
            { "Content-Type", new List<string?> { "application/json" } }
        };
        var routeParams = new Dictionary<string, object?> 
        { 
            { "resourceId", "123" } 
        };
        var queryParams = new List<KeyValuePair<string, List<string?>>>
        {
            new KeyValuePair<string, List<string?>>("format", new List<string?> { "json" })
        };

        var context = new AsyncContext<MyRequest>(request, headers, routeParams, queryParams);

        var handler = new ContextAwareHandler();

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        // Verify context data was used appropriately
    }
}
```

## Integration Testing

### Testing the Complete Flow

For integration testing, you can test the entire async endpoint flow:

```csharp
public class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public IntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostAsyncEndpoint_ReturnsJobId()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            Data = "test data",
            Priority = 1
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/process", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var jobInfo = await response.Content.ReadFromJsonAsync<Job>();
        Assert.NotNull(jobInfo);
        Assert.NotEqual(Guid.Empty, jobInfo.Id);
        Assert.Equal(JobStatus.Queued, jobInfo.Status);
    }

    [Fact]
    public async Task GetJobStatus_ReturnsJobDetails()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        // First create a job
        var request = new { Data = "test data", Priority = 1 };
        var createResponse = await client.PostAsJsonAsync("/api/process", request);
        var jobInfo = await createResponse.Content.ReadFromJsonAsync<Job>();
        
        // Wait a bit for processing
        await Task.Delay(100);

        // Act
        var statusResponse = await client.GetAsync($"/jobs/{jobInfo.Id}");

        // Assert
        statusResponse.EnsureSuccessStatusCode();
        var updatedJobInfo = await statusResponse.Content.ReadFromJsonAsync<Job>();
        Assert.NotNull(updatedJobInfo);
        Assert.Equal(jobInfo.Id, updatedJobInfo.Id);
    }
}
```

### Custom WebApplicationFactory

Create a custom factory for testing with specific configurations:

```csharp
public class TestWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace real storage with in-memory for testing
            services.RemoveAll<IJobStore>();
            services.AddSingleton<IJobStore, InMemoryJobStore>();
            
            // Replace real handlers with test handlers if needed
            services
                .AddAsyncEndpoints()
                .AddAsyncEndpointsInMemoryStore()
                .AddAsyncEndpointsWorker();
        });
    }
}
```

## Testing Storage

### Testing Job Store Implementations

Test your custom or built-in job store implementations:

```csharp
public class JobStoreTests
{
    private readonly IJobStore _jobStore;
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    
    public JobStoreTests()
    {
        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        _dateTimeProviderMock.Setup(d => d.DateTimeOffsetNow).Returns(DateTimeOffset.UtcNow);
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);
        
        // You can test with InMemoryJobStore or your custom implementation
        _jobStore = new InMemoryJobStore(_dateTimeProviderMock.Object);
    }

    [Fact]
    public async Task CreateJob_Succeeds_JobIsStored()
    {
        // Arrange
        var job = new Job();
        job.Name = "TestJob";
        job.Payload = "{}";

        // Act
        var result = await _jobStore.CreateJob(job, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        
        var retrieved = await _jobStore.GetJobById(job.Id, CancellationToken.None);
        Assert.True(retrieved.IsSuccess);
        Assert.Equal(job.Id, retrieved.Data.Id);
    }

    [Fact]
    public async Task ClaimNextJobForWorker_ReturnsAvailableJob()
    {
        // Arrange
        var job = new Job();
        job.Name = "TestJob";
        job.Payload = "{}";
        await _jobStore.CreateJob(job, CancellationToken.None);

        var workerId = Guid.NewGuid();

        // Act
        var claimedJob = await _jobStore.ClaimNextJobForWorker(workerId, CancellationToken.None);

        // Assert
        Assert.True(claimedJob.IsSuccess);
        Assert.NotNull(claimedJob.Data);
        Assert.Equal(workerId, claimedJob.Data.WorkerId);
        Assert.Equal(JobStatus.InProgress, claimedJob.Data.Status);
    }
}
```

## Testing Retry Logic

### Unit Testing Retry Behavior

Test your retry logic and backoff calculations:

```csharp
public class DelayCalculatorServiceTests
{
    [Theory]
    [InlineData(0, 2.0, 2.0)]  // 2^0 * 2.0 = 1 * 2.0 = 2.0 seconds
    [InlineData(1, 2.0, 4.0)]  // 2^1 * 2.0 = 2 * 2.0 = 4.0 seconds
    [InlineData(2, 2.0, 8.0)]  // 2^2 * 2.0 = 4 * 2.0 = 8.0 seconds
    [InlineData(3, 3.0, 24.0)] // 2^3 * 3.0 = 8 * 3.0 = 24.0 seconds
    public void CalculateRetryDelay_ReturnsCorrectDelay(int retryCount, double baseDelay, double expectedDelay)
    {
        // Arrange
        var config = new AsyncEndpointsJobManagerConfiguration 
        { 
            RetryDelayBaseSeconds = baseDelay 
        };
        
        // Act
        var delay = CalculateRetryDelay(retryCount, config.RetryDelayBaseSeconds);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(expectedDelay), delay);
    }

    private TimeSpan CalculateRetryDelay(int retryCount, double baseDelay)
    {
        return TimeSpan.FromSeconds(Math.Pow(2, retryCount) * baseDelay);
    }
}
```

## Testing Validation Middleware

Test your validation middleware:

```csharp
public class ValidationMiddlewareTests : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ValidationMiddlewareTests(TestWebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostWithInvalidData_ReturnsBadRequest()
    {
        // Arrange
        var invalidRequest = new { Data = "", Priority = 10 }; // invalid priority

        // Act
        var response = await _client.PostAsJsonAsync("/api/process", invalidRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostWithValidData_ReturnsAccepted()
    {
        // Arrange
        var validRequest = new { Data = "valid data", Priority = 1 };

        // Act
        var response = await _client.PostAsJsonAsync("/api/process", validRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}
```

## Testing Distributed Systems

### Mocking Redis Dependencies

When testing with Redis, you can provide mock implementations:

```csharp
public class RedisIntegrationTests
{
    [Fact]
    public async Task RedisStore_WithMockConnection_WorksCorrectly()
    {
        // For unit testing Redis store logic, mock IDatabase
        var mockDatabase = new Mock<IDatabase>();
        var mockLogger = new Mock<ILogger<RedisJobStore>>();
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        var jobHashConverter = new Mock<IJobHashConverter>();
        var serializer = new Mock<ISerializer>();
        var luaScriptService = new Mock<IRedisLuaScriptService>();
        
        var redisStore = new RedisJobStore(
            mockLogger.Object, 
            mockDatabase.Object, 
            dateTimeProvider.Object,
            jobHashConverter.Object,
            serializer.Object,
            luaScriptService.Object
        );
        
        // Test your logic...
    }
}
```

## Test Utilities

### Test Job Builder

Create a utility for building test jobs:

```csharp
public static class TestJobBuilder
{
    public static Job CreateQueuedJob(string name = "TestJob", string payload = "{}")
    {
        var job = new Job
        {
            Name = name,
            Payload = payload,
            Status = JobStatus.Queued
        };
        
        return job;
    }
    
    public static Job CreateInProgressJob(Guid workerId, string name = "TestJob")
    {
        var job = CreateQueuedJob(name);
        job.Status = JobStatus.InProgress;
        job.WorkerId = workerId;
        job.StartedAt = DateTimeOffset.UtcNow;
        
        return job;
    }
}
```

## Testing Best Practices

### 1. Isolate Test Concerns

Test different layers separately:

```csharp
// Unit test for business logic
[Fact]
public async Task Handler_BusinessLogic_ReturnsExpectedResult()
{
    // Test only the handler's business logic
}

// Integration test for endpoint
[Fact]
public async Task Endpoint_EndToEnd_FullFlowWorks()
{
    // Test the full flow from HTTP request to job completion
}
```

### 2. Use Appropriate Test Doubles

- **Mocks**: For dependencies with behavior you need to verify
- **Stubs**: For dependencies that return data but don't need verification
- **Fakes**: For simpler implementations of complex dependencies

### 3. Test Error Conditions

```csharp
[Fact]
public async Task Handler_WithExternalServiceFailure_ReturnsAppropriateError()
{
    // Arrange
    var mockService = new Mock<IExternalService>();
    mockService.Setup(s => s.ProcessAsync(It.IsAny<MyRequest>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException());

    var handler = new MyHandler(mockService.Object);

    // Act
    var result = await handler.HandleAsync(testContext, CancellationToken.None);

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Equal("EXTERNAL_SERVICE_ERROR", result.Error.Code);
}
```

### 4. Test Concurrency Scenarios

```csharp
[Fact]
public async Task MultipleWorkers_ClaimJobs_DoesNotCauseConflicts()
{
    // Test that multiple workers can safely claim different jobs
    var store = new InMemoryJobStore(new DateTimeProvider());
    
    // Create multiple jobs
    var job1 = TestJobBuilder.CreateQueuedJob("Job1");
    var job2 = TestJobBuilder.CreateQueuedJob("Job2");
    await store.CreateJob(job1, CancellationToken.None);
    await store.CreateJob(job2, CancellationToken.None);

    // Simulate multiple workers claiming jobs
    var worker1Task = store.ClaimNextJobForWorker(Guid.NewGuid(), CancellationToken.None);
    var worker2Task = store.ClaimNextJobForWorker(Guid.NewGuid(), CancellationToken.None);
    
    var results = await Task.WhenAll(worker1Task, worker2Task);
    
    // Verify both jobs were claimed successfully
    Assert.NotNull(results[0].Data);
    Assert.NotNull(results[1].Data);
    Assert.NotEqual(results[0].Data.Id, results[1].Data.Id);
}
```

### 5. Test Retry Scenarios

```csharp
[Fact]
public async Task FailedJob_WithRetriesAvailable_BecomesScheduled()
{
    // Arrange
    var job = TestJobBuilder.CreateInProgressJob(Guid.NewGuid());
    job.RetryCount = 1; // Has one retry used
    job.MaxRetries = 3; // Still has retries left
    var store = new InMemoryJobStore(new DateTimeProvider());
    await store.CreateJob(job, CancellationToken.None);
    
    var jobManager = new JobManager(store, Mock.Of<ILogger<JobManager>>(), 
        Options.Create(new AsyncEndpointsConfigurations()), new DateTimeProvider());

    // Act
    var error = AsyncEndpointError.FromMessage("Processing failed");
    var result = await jobManager.ProcessJobFailure(job.Id, error, CancellationToken.None);

    // Assert
    Assert.True(result.IsSuccess);
    
    var updatedJob = await store.GetJobById(job.Id, CancellationToken.None);
    Assert.Equal(JobStatus.Scheduled, updatedJob.Data.Status);
    Assert.Equal(2, updatedJob.Data.RetryCount); // Incremented
}
```

## Performance Testing

Consider performance aspects in your tests:

```csharp
[Fact]
public async Task Handler_Performance_UnderThreshold()
{
    // Arrange
    var handler = new MyHandler();
    var context = CreateTestContext();

    // Act
    var stopwatch = Stopwatch.StartNew();
    var result = await handler.HandleAsync(context, CancellationToken.None);
    stopwatch.Stop();

    // Assert
    Assert.True(result.IsSuccess);
    Assert.True(stopwatch.ElapsedMilliseconds < 1000, "Handler should complete within 1 second");
}
```

Testing AsyncEndpoints applications requires careful attention to both the synchronous API and the asynchronous background processing. With proper testing strategies, you can ensure your async workflows are reliable and robust.