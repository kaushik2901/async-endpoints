# AsyncEndpoints Observability Enhancement

## Introduction

The AsyncEndpoints library currently has robust logging capabilities but lacks comprehensive metrics and tracing support. This document outlines the enhancement plan to make the library observation-rich with the highest quality standards for metrics and distributed tracing.

## Current State

The library currently implements extensive logging using Microsoft.Extensions.Logging with structured logging and scopes. Key areas already instrumented include:

- Job lifecycle management (creation, claiming, processing, completion)
- Background service operations
- Handler execution
- Job store operations (InMemoryJobStore and RedisJobStore)
- Request processing

## Problem Statement

The current metrics approach described in the documentation has poor developer experience due to:
1. Direct usage of static metric instruments scattered throughout the codebase
2. Lack of proper abstraction layer between business logic and metrics system
3. Poor testability due to static dependencies
4. Difficulty in customizing, disabling, or configuring metrics
5. No centralized approach to metric management

## Improved Approach: Proper Abstractions for Metrics

### 1. OpenTelemetry Integration with Dependency Injection

#### 1.1. Interface-Based Metrics Abstraction

Instead of using static metric instruments, create an interface-based abstraction that can be injected into services:

```csharp
public interface IAsyncEndpointsObservability
{
    // Job metrics
    void RecordJobCreated(string jobName, string storeType);
    void RecordJobProcessed(string jobName, string status, string storeType);
    void RecordJobFailed(string jobName, string errorType, string storeType);
    void RecordJobRetries(string jobName, string storeType);
    void RecordJobQueueDuration(string jobName, string storeType, double durationSeconds);
    void RecordJobProcessingDuration(string jobName, string status, double durationSeconds);
    void RecordJobClaimDuration(string storeType, double durationSeconds);
    void SetJobCurrentCount(string jobStatus, string storeType, long count);
    
    // Handler metrics  
    void RecordHandlerExecutionDuration(string jobName, string handlerType, double durationSeconds);
    void RecordHandlerError(string jobName, string errorType);
    
    // Store metrics
    void RecordStoreOperation(string operation, string storeType);
    void RecordStoreOperationDuration(string operation, string storeType, double durationSeconds);
    void RecordStoreError(string operation, string errorType, string storeType);
    
    // Background service metrics
    void RecordBackgroundProcessingRate(string workerId);
    void RecordBackgroundConsumerIdleTime(string workerId, double durationSeconds);
    void SetBackgroundChannelUtilization(string channelType, double utilizationPercentage);
    
    // Duration tracking methods
    /// <summary>
    /// Records duration of job processing using a disposable timer
    /// </summary>
    /// <param name="jobName">Name of the job</param>
    /// <param name="status">Status of the job</param>
    /// <returns>IDisposable timer that records duration when disposed</returns>
    IDisposable TimeJobProcessingDuration(string jobName, string status);
    
    /// <summary>
    /// Records duration of a handler execution
    /// </summary>
    /// <param name="jobName">Name of the job</param>
    /// <param name="handlerType">Type of the handler</param>
    /// <returns>IDisposable timer that records duration when disposed</returns>
    IDisposable TimeHandlerExecution(string jobName, string handlerType);

    // Activity/tracing methods
    /// <summary>
    /// Starts a job submission activity if tracing is enabled
    /// </summary>
    /// <param name="jobName">Name of the job</param>
    /// <param name="storeType">The type of store</param>
    /// <param name="jobId">ID of the job</param>
    /// <returns>An Activity if tracing is enabled, otherwise null</returns>
    Activity? StartJobSubmitActivity(string jobName, string storeType, Guid jobId);
    
    /// <summary>
    /// Starts a job processing activity if tracing is enabled
    /// </summary>
    /// <param name="storeType">The type of store</param>
    /// <param name="job">The job being processed</param>
    /// <returns>An Activity if tracing is enabled, otherwise null</returns>
    Activity? StartJobProcessActivity(string storeType, Job job);
    
    /// <summary>
    /// Starts a handler execution activity if tracing is enabled
    /// </summary>
    /// <param name="jobName">Name of the job</param>
    /// <param name="jobId">ID of the job</param>
    /// <param name="handlerType">Type of the handler being executed</param>
    /// <returns>An Activity if tracing is enabled, otherwise null</returns>
    Activity? StartHandlerExecuteActivity(string jobName, Guid jobId, string handlerType);
    
    /// <summary>
    /// Starts a store operation activity if tracing is enabled
    /// </summary>
    /// <param name="operation">The store operation being performed</param>
    /// <param name="storeType">The type of store</param>
    /// <param name="jobId">Optional job ID associated with the operation</param>
    /// <returns>An Activity if tracing is enabled, otherwise null</returns>
    Activity? StartStoreOperationActivity(string operation, string storeType, Guid? jobId = null);
}
```

#### 1.2. Implementation of Metrics Interface

Create an implementation that properly manages the metric instruments using the Meter class directly:

```csharp
public class AsyncEndpointsObservability : IAsyncEndpointsObservability
{
    private readonly Meter _meter;
    private readonly Counter<long> _jobsCreated;
    private readonly Counter<long> _jobsProcessed;
    private readonly Counter<long> _jobsFailed;
    private readonly Counter<long> _jobsRetries;
    private readonly Histogram<double> _jobQueueDuration;
    private readonly Histogram<double> _jobProcessingDuration;
    private readonly Histogram<double> _jobClaimDuration;
    private readonly Gauge<long> _jobsCurrentCount;
    private readonly Histogram<double> _handlerExecutionDuration;
    private readonly Counter<long> _handlerErrors;
    private readonly Counter<long> _storeOperations;
    private readonly Histogram<double> _storeOperationDuration;
    private readonly Counter<long> _storeErrors;
    private readonly Counter<long> _backgroundProcessingRate;
    private readonly Histogram<double> _backgroundConsumerIdleTime;
    private readonly Gauge<double> _backgroundChannelUtilization;
    private readonly AsyncEndpointsObservabilityConfigurations _options;
    private readonly ILogger<AsyncEndpointsObservability> _logger;
    
    public AsyncEndpointsObservability(IOptions<AsyncEndpointsConfigurations> configurations, ILogger<AsyncEndpointsObservability> logger)
    {
        _options = configurations.Value.ObservabilityConfigurations;
        _logger = logger;
        
        // Only create metrics if observability is enabled
        if (_options.EnableMetrics)
        {
            _meter = new Meter("AsyncEndpoints", "1.0.0");
            
            // Job metrics
            _jobsCreated = _meter.CreateCounter<long>("asyncendpoints.jobs.created.total", 
                description: "Total number of jobs created");
            _jobsProcessed = _meter.CreateCounter<long>("asyncendpoints.jobs.processed.total", 
                description: "Total number of jobs processed");
            _jobsFailed = _meter.CreateCounter<long>("asyncendpoints.jobs.failed.total", 
                description: "Total number of job failures");
            _jobsRetries = _meter.CreateCounter<long>("asyncendpoints.jobs.retries.total", 
                description: "Total number of job retries");
            _jobQueueDuration = _meter.CreateHistogram<double>("asyncendpoints.jobs.queue.duration", 
                unit: "seconds", description: "Time jobs spend in queue before processing");
            _jobProcessingDuration = _meter.CreateHistogram<double>("asyncendpoints.jobs.processing.duration", 
                unit: "seconds", description: "Time spent processing jobs");
            _jobClaimDuration = _meter.CreateHistogram<double>("asyncendpoints.jobs.claim.duration", 
                unit: "seconds", description: "Time taken to claim jobs");
            _jobsCurrentCount = _meter.CreateGauge<long>("asyncendpoints.jobs.current.count", 
                description: "Current number of jobs in each state");
            
            // Handler metrics
            _handlerExecutionDuration = _meter.CreateHistogram<double>("asyncendpoints.handlers.execution.duration", 
                unit: "seconds", description: "Time spent executing handlers");
            _handlerErrors = _meter.CreateCounter<long>("asyncendpoints.handlers.error.rate", 
                description: "Count of handler execution errors");
            
            // Store metrics
            _storeOperations = _meter.CreateCounter<long>("asyncendpoints.store.operations.total", 
                description: "Total store operations by operation type");
            _storeOperationDuration = _meter.CreateHistogram<double>("asyncendpoints.store.operation.duration", 
                unit: "seconds", description: "Duration of store operations");
            _storeErrors = _meter.CreateCounter<long>("asyncendpoints.store.errors.total", 
                description: "Count of store operation errors");
            
            // Background service metrics
            _backgroundProcessingRate = _meter.CreateCounter<long>("asyncendpoints.background.processing.rate", 
                description: "Rate of job processing");
            _backgroundConsumerIdleTime = _meter.CreateHistogram<double>("asyncendpoints.background.consumer.idle.time", 
                unit: "seconds", description: "Time consumers spend idle");
            _backgroundChannelUtilization = _meter.CreateGauge<double>("asyncendpoints.background.channel.utilization", 
                description: "Channel utilization percentage");
        }
        else
        {
            // Initialize as null when metrics are disabled
            _meter = null;
            _jobsCreated = null;
            _jobsProcessed = null;
            _jobsFailed = null;
            _jobsRetries = null;
            _jobQueueDuration = null;
            _jobProcessingDuration = null;
            _jobClaimDuration = null;
            _jobsCurrentCount = null;
            _handlerExecutionDuration = null;
            _handlerErrors = null;
            _storeOperations = null;
            _storeOperationDuration = null;
            _storeErrors = null;
            _backgroundProcessingRate = null;
            _backgroundConsumerIdleTime = null;
            _backgroundChannelUtilization = null;
        }
    }

    public void RecordJobCreated(string jobName, string storeType)
    {
        if (_options.EnableMetrics && _jobsCreated != null)
        {
            _jobsCreated.Add(1, new("job_name", jobName), new("store_type", storeType));
        }
    }

    public void RecordJobProcessed(string jobName, string status, string storeType)
    {
        if (_options.EnableMetrics && _jobsProcessed != null)
        {
            _jobsProcessed.Add(1, new("job_name", jobName), new("status", status), new("store_type", storeType));
        }
    }

    public void RecordJobFailed(string jobName, string errorType, string storeType)
    {
        if (_options.EnableMetrics && _jobsFailed != null)
        {
            _jobsFailed.Add(1, new("job_name", jobName), new("error_type", errorType), new("store_type", storeType));
        }
    }

    public void RecordJobRetries(string jobName, string storeType)
    {
        if (_options.EnableMetrics && _jobsRetries != null)
        {
            _jobsRetries.Add(1, new("job_name", jobName), new("store_type", storeType));
        }
    }

    public void RecordJobQueueDuration(string jobName, string storeType, double durationSeconds)
    {
        if (_options.EnableMetrics && _jobQueueDuration != null)
        {
            _jobQueueDuration.Record(durationSeconds, new("job_name", jobName), new("store_type", storeType));
        }
    }

    public void RecordJobProcessingDuration(string jobName, string status, double durationSeconds)
    {
        if (_options.EnableMetrics && _jobProcessingDuration != null)
        {
            _jobProcessingDuration.Record(durationSeconds, new("job_name", jobName), new("status", status));
        }
    }

    public void RecordJobClaimDuration(string storeType, double durationSeconds)
    {
        if (_options.EnableMetrics && _jobClaimDuration != null)
        {
            _jobClaimDuration.Record(durationSeconds, new("store_type", storeType));
        }
    }

    public void SetJobCurrentCount(string jobStatus, string storeType, long count)
    {
        if (_options.EnableMetrics && _jobsCurrentCount != null)
        {
            _jobsCurrentCount.Set(count, new("job_status", jobStatus), new("store_type", storeType));
        }
    }

    public void RecordHandlerExecutionDuration(string jobName, string handlerType, double durationSeconds)
    {
        if (_options.EnableMetrics && _handlerExecutionDuration != null)
        {
            _handlerExecutionDuration.Record(durationSeconds, new("job_name", jobName), new("handler_type", handlerType));
        }
    }

    public void RecordHandlerError(string jobName, string errorType)
    {
        if (_options.EnableMetrics && _handlerErrors != null)
        {
            _handlerErrors.Add(1, new("job_name", jobName), new("error_type", errorType));
        }
    }

    public void RecordStoreOperation(string operation, string storeType)
    {
        if (_options.EnableMetrics && _storeOperations != null)
        {
            _storeOperations.Add(1, new("operation", operation), new("store_type", storeType));
        }
    }

    public void RecordStoreOperationDuration(string operation, string storeType, double durationSeconds)
    {
        if (_options.EnableMetrics && _storeOperationDuration != null)
        {
            _storeOperationDuration.Record(durationSeconds, new("operation", operation), new("store_type", storeType));
        }
    }

    public void RecordStoreError(string operation, string errorType, string storeType)
    {
        if (_options.EnableMetrics && _storeErrors != null)
        {
            _storeErrors.Add(1, new("operation", operation), new("error_type", errorType), new("store_type", storeType));
        }
    }

    public void RecordBackgroundProcessingRate(string workerId)
    {
        if (_options.EnableMetrics && _backgroundProcessingRate != null)
        {
            _backgroundProcessingRate.Add(1, new("worker_id", workerId));
        }
    }

    public void RecordBackgroundConsumerIdleTime(string workerId, double durationSeconds)
    {
        if (_options.EnableMetrics && _backgroundConsumerIdleTime != null)
        {
            _backgroundConsumerIdleTime.Record(durationSeconds, new("worker_id", workerId));
        }
    }

    public void SetBackgroundChannelUtilization(string channelType, double utilizationPercentage)
    {
        if (_options.EnableMetrics && _backgroundChannelUtilization != null)
        {
            _backgroundChannelUtilization.Set(utilizationPercentage, new("channel_type", channelType));
        }
    }

    public IDisposable TimeJobProcessingDuration(string jobName, string status)
    {
        if (_options.EnableMetrics && _jobProcessingDuration != null)
        {
            return MetricTimer.Start(duration => _jobProcessingDuration.Record(duration, new("job_name", jobName), new("status", status)));
        }
        
        return NullDisposable.Instance; // Return a no-op disposable when metrics are disabled
    }
    
    public IDisposable TimeHandlerExecution(string jobName, string handlerType)
    {
        if (_options.EnableMetrics && _handlerExecutionDuration != null)
        {
            return MetricTimer.Start(duration => _handlerExecutionDuration.Record(duration, new("job_name", jobName), new("handler_type", handlerType)));
        }
        
        return NullDisposable.Instance; // Return a no-op disposable when metrics are disabled
    }

    private static readonly ActivitySource _activitySource = new ActivitySource("AsyncEndpoints", "1.0.0");

    public Activity? StartJobSubmitActivity(string jobName, string storeType, Guid jobId)
    {
        if (_options.EnableTracing)
        {
            var activity = _activitySource.StartActivity("Job.Submit", ActivityKind.Server);
            activity?.SetTag("job.id", jobId.ToString());
            activity?.SetTag("job.name", jobName);
            activity?.SetTag("store.type", storeType);
            return activity;
        }
        return null; // Return null when tracing is disabled
    }

    public Activity? StartJobProcessActivity(string storeType, Job job)
    {
        if (_options.EnableTracing)
        {
            var activity = _activitySource.StartActivity("Job.Process", ActivityKind.Consumer);
            activity?.SetTag("job.id", job.Id.ToString());
            activity?.SetTag("job.name", job.Name);
            activity?.SetTag("job.status", job.Status.ToString());
            activity?.SetTag("worker.id", job.WorkerId?.ToString());
            activity?.SetTag("store.type", storeType);
            return activity;
        }
        return null; // Return null when tracing is disabled
    }

    public Activity? StartHandlerExecuteActivity(string jobName, Guid jobId, string handlerType)
    {
        if (_options.EnableTracing)
        {
            var activity = _activitySource.StartActivity("Handler.Execute", ActivityKind.Internal);
            activity?.SetTag("job.id", jobId.ToString());
            activity?.SetTag("job.name", jobName);
            activity?.SetTag("handler.type", handlerType);
            return activity;
        }
        return null; // Return null when tracing is disabled
    }

    public Activity? StartStoreOperationActivity(string operation, string storeType, Guid? jobId = null)
    {
        if (_options.EnableTracing)
        {
            var activity = _activitySource.StartActivity("Store.Operation", ActivityKind.Internal);
            activity?.SetTag("operation", operation);
            activity?.SetTag("store.type", storeType);
            if (jobId.HasValue)
            {
                activity?.SetTag("job.id", jobId.Value.ToString());
            }
            return activity;
        }
        return null; // Return null when tracing is disabled
    }
}
```



### 2. Helper Classes

#### 2.1. MetricTimer Helper Class

```csharp
public class MetricTimer : IDisposable
{
    private readonly Action<double> _onDispose;
    private readonly Stopwatch _stopwatch;

    private MetricTimer(Action<double> onDispose)
    {
        _onDispose = onDispose;
        _stopwatch = Stopwatch.StartNew();
    }

    public void Dispose()
    {
        _stopwatch.Stop();
        var duration = _stopwatch.Elapsed.TotalSeconds;
        _onDispose(duration);
    }

    public static MetricTimer Start(Action<double> onDurationRecorded)
    {
        return new MetricTimer(onDurationRecorded);
    }
}
```

#### 2.2. NullDisposable Helper Class

```csharp
// Helper class for no-op disposable in the AsyncEndpoints.Utilities namespace
namespace AsyncEndpoints.Utilities
{
    internal class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new NullDisposable();
        
        public void Dispose()
        {
            // No-op
        }
    }
}
```

### 3. Implementation Plan

#### 3.1. Service Registration

The observability services (IAsyncEndpointsObservability and AsyncEndpointsObservability) are automatically registered when AddAsyncEndpoints is called, eliminating the need for a separate extension method. The services are registered as singletons and will be available whenever AsyncEndpoints is added to the service collection.

#### 3.2. Using Metrics and Tracing in Services

With the new abstractions, services can now use dependency injection for metrics and tracing. Here's an example with JobManager:

```csharp
public class JobManager(
    IJobStore jobStore, 
    ILogger<JobManager> logger, 
    IOptions<AsyncEndpointsConfigurations> options, 
    IDateTimeProvider dateTimeProvider,
    IAsyncEndpointsObservability metrics) : IJobManager
{
    private readonly ILogger<JobManager> _logger = logger;
    private readonly IJobStore _jobStore = jobStore;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly AsyncEndpointsJobManagerConfiguration _jobManagerConfiguration = options.Value.JobManagerConfiguration;
    private readonly IAsyncEndpointsObservability _metrics = metrics;

    public async Task<MethodResult<Job>> SubmitJob(string jobName, string payload, HttpContext httpContext, CancellationToken cancellationToken)
    {
        using var _ = _logger.BeginScope(new { JobName = jobName });
        
        var id = httpContext.GetOrCreateJobId();
        
        // Start activity only if tracing is enabled
        using var activity = _metrics.StartJobSubmitActivity(jobName, _jobStore.GetType().Name, id);
        
        // Use disposable timer to measure total duration
        using var durationTimer = _metrics.TimeJobProcessingDuration(jobName, "created");
        
        _logger.LogDebug("Processing job creation for: {JobName}, payload length: {PayloadLength}", jobName, payload.Length);
        
        var result = await _jobStore.GetJobById(id, cancellationToken);
        if (result.IsSuccess && result.Data != null)
        {
            _logger.LogDebug("Found existing job {JobId} for job: {JobName}, returning existing job", id, jobName);
            return MethodResult<Job>.Success(result.Data);
        }

        var headers = httpContext.GetHeadersFromContext();
        var routeParams = httpContext.GetRouteParamsFromContext();
        var queryParams = httpContext.GetQueryParamsFromContext();

        var job = Job.Create(id, jobName, payload, headers, routeParams, queryParams, _dateTimeProvider);
        _logger.LogDebug("Created new job {JobId} for job: {JobName}", id, jobName);

        var createJobResult = await _jobStore.CreateJob(job, cancellationToken);
        if (createJobResult.IsSuccess)
        {
            _metrics.RecordJobCreated(jobName, _jobStore.GetType().Name);
            
            _logger.LogDebug("Successfully created job {JobId} in store", id);
            return MethodResult<Job>.Success(job);
        }
        else
        {
            _logger.LogError("Failed to create job {JobId} in store: {Error}", id, createJobResult.Error?.Message);
            return MethodResult<Job>.Failure(createJobResult.Error!);
        }
    }
}
```

Here's how JobProcessorService would use the metrics interface:

```csharp
public async Task ProcessAsync(Job job, CancellationToken cancellationToken)
{
    using var _ = _logger.BeginScope(new { JobId = job.Id, JobName = job.Name });
    
    _logger.LogDebug("Starting job processing for job {JobId} with name {JobName}", job.Id, job.Name);

    // Start activity only if tracing is enabled
    // NOTE: In actual implementation, storeType would need to be obtained from job metadata 
    // or passed from the calling service that has access to the store
    using var activity = _metrics.StartJobProcessActivity(job.StoreType ?? "Unknown", job);
    
    // Use the enhanced metrics interface for cleaner duration tracking
    using var durationTimer = _metrics.TimeJobProcessingDuration(job.Name, "processed");
    
    try
    {
        var result = await ProcessJobPayloadAsync(job, cancellationToken);
        if (!result.IsSuccess)
        {
            activity?.SetStatus(ActivityStatusCode.Error, result.Error.Message);
            activity?.SetTag("error.type", result.Error.Code);
            
            _logger.LogError("Failed to process job {JobId}: {Error}", job.Id, result.Error.Message);
            
            var processJobFailureResult = await _jobManager.ProcessJobFailure(job.Id, result.Error, cancellationToken);
            if (!processJobFailureResult.IsSuccess)
            {
                _logger.LogError("Failed to update job status for failure {JobId}: {Error}", job.Id, processJobFailureResult.Error.Message);
                return;
            }

            return;
        }

        var processJobSuccessResult = await _jobManager.ProcessJobSuccess(job.Id, result.Data, cancellationToken);
        if (!processJobSuccessResult.IsSuccess)
        {
            activity?.SetStatus(ActivityStatusCode.Error, processJobSuccessResult.Error.Message);
            
            _logger.LogError("Failed to update job status for success {JobId}: {Error}", job.Id, processJobSuccessResult.Error.Message);
            return;
        }

        _logger.LogInformation("Successfully processed job {JobId}", job.Id);
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.SetTag("error.type", ex.GetType().Name);
        
        _logger.LogError(ex, "Exception occurred during job processing");
    }
}
```

### 4. Configuration Options

Provide configuration options for observability:

```csharp
/// <summary>
/// Configuration settings for AsyncEndpoints observability features.
/// </summary>
public sealed class AsyncEndpointsObservabilityConfigurations
{
    /// <summary>
    /// Gets or sets a value indicating whether metrics collection is enabled.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether distributed tracing is enabled.
    /// </summary>
    public bool EnableTracing { get; set; } = true;
}
```

### 5. Developer Experience Improvements

#### 5.1. Testability
The new interface-based approach allows for easy mocking of observability in unit tests using AutoFixture and AutoMoq:

```csharp
/// <summary>
/// Verifies that when a new job is submitted, the observability interface records the job creation metric.
/// This ensures proper metric collection for monitoring job creation rates.
/// </summary>
[Theory, AutoMoqData]
public async Task SubmitJob_CreatesNewJob_RecordsJobCreatedMetric(
    string jobName,
    string payload,
    Mock<IJobStore> mockJobStore,
    Mock<ILogger<JobManager>> mockLogger,
    Mock<IOptions<AsyncEndpointsConfigurations>> mockOptions,
    Mock<IDateTimeProvider> mockDateTimeProvider,
    Mock<IAsyncEndpointsObservability> mockMetrics,
    Mock<HttpContext> mockHttpContext,
    Guid jobId)
{
    // Arrange
    mockHttpContext.Setup(ctx => ctx.GetOrCreateJobId()).Returns(jobId);
    mockJobStore.Setup(store => store.GetJobById(jobId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(MethodResult<Job>.Success(null)); // No existing job found
    mockJobStore.Setup(store => store.CreateJob(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(MethodResult.Success);

    var jobManager = new JobManager(
        mockJobStore.Object,
        mockLogger.Object,
        mockOptions.Object,
        mockDateTimeProvider.Object,
        mockMetrics.Object);

    // Act
    await jobManager.SubmitJob(jobName, payload, mockHttpContext.Object, CancellationToken.None);

    // Assert
    mockMetrics.Verify(m => m.RecordJobCreated(jobName, It.IsAny<string>()), Times.Once);
}
```



### 6. Performance Considerations

1. **Minimal Overhead**: The interface abstraction has negligible performance impact
2. **Dependency Injection**: Metrics services are singletons, eliminating instantiation overhead
3. **Batching**: OpenTelemetry handles metric batching and export automatically
4. **Configurable**: Can be disabled entirely when not needed

### 7. Testing Strategy

1. **Unit Tests**: Verify metrics are recorded by checking method calls on the interface
2. **Integration Tests**: Validate that traces are properly propagated using test activity listeners
3. **Performance Tests**: Ensure observability doesn't significantly impact performance
4. **Compatibility Tests**: Test with various OpenTelemetry exporters and backends

### 8. Backward Compatibility

The observability enhancements will be:
- **Opt-in**: Disabled by default to maintain existing behavior
- **Non-breaking**: Existing logging functionality remains unchanged
- **Configurable**: Allow users to enable/disable specific observability features
- **Replaceable**: Developers can provide their own implementations of the metrics interface

### 9. Documentation

Update documentation to include:
- Setup instructions for OpenTelemetry integration
- Configuration examples for different backends
- Best practices for monitoring and alerting
- Troubleshooting guides
- Examples of custom metric implementations

### 10. Example Usage

After implementation, users can enable observability with:

```csharp
// Basic setup with AsyncEndpoints observability
services.AddAsyncEndpoints(options =>
        {
            options.ObservabilityConfigurations.EnableMetrics = true;
            options.ObservabilityConfigurations.EnableTracing = true;
        })
        .AddAsyncEndpointsInMemoryStore();

// Configure OpenTelemetry separately for export
services.AddOpenTelemetry()
        .WithMetrics(config =>
        {
            config.AddAspNetCoreInstrumentation()
                  .AddRuntimeInstrumentation()
                  .AddMeter("AsyncEndpoints"); // Subscribe to AsyncEndpoints metrics
        })
        .WithTracing(config =>
        {
            config.AddAspNetCoreInstrumentation()
                  .AddSource("AsyncEndpoints"); // Subscribe to AsyncEndpoints traces
        });

// Or with custom observability implementation
services.AddSingleton<IAsyncEndpointsObservability, CustomObservability>();
```

### 11. Trace Context Propagation and Span Attributes

#### 11.1. Trace Context Propagation

The library will implement distributed tracing by:

1. **Request Initiation**: When a job is created via the endpoint, the current trace context (Span/Trace ID) will be captured and stored with the job metadata if tracing is enabled
2. **Job Processing**: When a background worker processes the job, it will create a new span as a child of the original trace context if tracing is enabled
3. **Continuity**: This ensures end-to-end traceability from the initial request to job completion when tracing is enabled

#### 11.2. Span Attributes

When tracing is enabled, each span will include relevant attributes:

- `job.id`: Unique job identifier
- `job.name`: Job type/name
- `job.status`: Current job status
- `worker.id`: Worker processing the job
- `store.type`: Job store implementation (InMemory, Redis)
- `handler.type`: Handler being executed
- `retry.count`: Current retry count for failed jobs
- `error.type`: Classification of any errors

#### 11.3. Where to Start Activities (Distributed Tracing)

Based on analysis of the AsyncEndpoints solution, here are the essential locations where Activities should be started for end-to-end traceability when tracing is enabled, balancing comprehensive monitoring with performance:

**1. JobManager.SubmitJob method** - When a new job is created via an endpoint (this is the initial trace entry point)
```csharp
// In JobManager.SubmitJob method using the observability interface
using var activity = _metrics.StartJobSubmitActivity(jobName, _jobStore.GetType().Name, id);
```

**2. JobProcessorService.ProcessAsync method** - When a background worker processes a job (continues the trace from the initial request)
```csharp
// In JobProcessorService.ProcessAsync method using the observability interface
// NOTE: In actual implementation, storeType would need to be obtained from job metadata 
// or passed from the calling service that has access to the store
using var activity = _metrics.StartJobProcessActivity(job.StoreType ?? "Unknown", job);
```

**3. Handler execution** - When the actual business logic is executed (shows the core work being done)
```csharp
// Using the observability interface in ProcessJobPayloadAsync method or IHandlerExecutionService.ExecuteHandlerAsync
using var activity = _metrics.StartHandlerExecuteActivity(job.Name, job.Id, handlerRegistration?.HandlerType?.Name);
```

**4. Key store operations** - Critical operations that bridge the request and processing (CreateJob and ClaimNextJob, but not read operations which could be too noisy)
```csharp
// Using the observability interface in CreateJob method
using var activity = _metrics.StartStoreOperationActivity("CreateJob", _jobStore.GetType().Name, job?.Id);

// Using the observability interface in ClaimNextJobForWorker method
using var activity = _metrics.StartStoreOperationActivity("ClaimJob", _jobStore.GetType().Name, jobId);
```

These essential Activity spans provide end-to-end traceability from the initial request creating a job through to its background processing completion when tracing is enabled, while maintaining a good balance between observability and performance overhead. When tracing is disabled, these methods return null and no activities are created, minimizing performance impact.

#### 11.4. Testing with Mock Observability

In unit tests, you can easily mock the observability interface using AutoFixture and AutoMoq:

```csharp
public class JobManagerTests
{
    /// <summary>
    /// Verifies that when a new job is submitted, the observability interface records the job creation metric.
    /// This ensures proper metric collection for monitoring job creation rates.
    /// </summary>
    [Theory, AutoMoqData]
    public async Task SubmitJob_CreatesNewJob_RecordsJobCreatedMetric(
        string jobName,
        string payload,
        Mock<IJobStore> mockJobStore,
        Mock<ILogger<JobManager>> mockLogger,
        Mock<IOptions<AsyncEndpointsConfigurations>> mockOptions,
        Mock<IDateTimeProvider> mockDateTimeProvider,
        Mock<IAsyncEndpointsObservability> mockMetrics,
        Mock<HttpContext> mockHttpContext,
        Guid jobId)
    {
        // Arrange
        mockHttpContext.Setup(ctx => ctx.GetOrCreateJobId()).Returns(jobId);
        mockJobStore.Setup(store => store.GetJobById(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MethodResult<Job>.Success(null)); // No existing job found
        mockJobStore.Setup(store => store.CreateJob(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MethodResult.Success);

        var jobManager = new JobManager(
            mockJobStore.Object,
            mockLogger.Object,
            mockOptions.Object,
            mockDateTimeProvider.Object,
            mockMetrics.Object);

        // Act
        await jobManager.SubmitJob(jobName, payload, mockHttpContext.Object, CancellationToken.None);

        // Assert
        mockMetrics.Verify(m => m.RecordJobCreated(jobName, It.IsAny<string>()), Times.Once);
    }
}

## Conclusion

This improved observability enhancement provides AsyncEndpoints users with comprehensive monitoring capabilities with significantly better developer experience. The implementation uses interface-based abstractions that:

1. Enable easy testing with mock implementations
2. Allow for customization and extension
3. Decouple business logic from metrics implementation
4. Follow dependency injection best practices
5. Maintain backward compatibility 
6. Provide flexibility for various deployment scenarios

The library will become significantly more observable without impacting existing functionality, allowing users to better monitor, debug, and optimize their async job processing workflows while providing a much better developer experience for metrics integration.