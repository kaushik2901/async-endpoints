# Technical Design Document: Observability Architecture Improvement for AsyncEndpoints

## Table of Contents
1. [Overview](#overview)
2. [Current State Analysis](#current-state-analysis)
3. [Issues with Current Implementation](#issues-with-current-implementation)
4. [Proposed Architecture](#proposed-architecture)
5. [New Interfaces and Abstractions](#new-interfaces-and-abstractions)
6. [Implementation Strategy](#implementation-strategy)
7. [Testing Strategy](#testing-strategy)
8. [Challenges](#challenges)

## Overview

This document outlines the proposed improvements to the observability architecture in the AsyncEndpoints library. The current implementation provides basic metrics and tracing capabilities but lacks a clean, extensible architecture that allows for better separation of concerns and customization.

### Goals
- Improve the architecture for metrics and tracing
- Provide better separation of concerns
- Enable extensibility for custom observability providers
- Follow .NET best practices for observability

## Current State Analysis

The current observability implementation consists of:
- `IAsyncEndpointsObservability` interface with 20+ methods
- `AsyncEndpointsObservability` implementation using System.Diagnostics.Metrics and Activity
- `MetricTimer` for duration tracking
- Direct injection of observability service into multiple components
- Metrics and tracing enabled/disabled via configuration

### Components Using Current Observability
- JobManager
- JobProcessorService
- InMemoryJobStore
- RedisJobStore
- HandlerExecutionService
- JobConsumerService

## Issues with Current Implementation

### 1. Interface Bloat
The `IAsyncEndpointsObservability` interface has 20+ methods, violating the Interface Segregation Principle. This makes it difficult to implement custom observability providers.

### 2. Tight Coupling
Components are tightly coupled to the specific observability implementation, making it difficult to:
- Replace with different observability backends
- Test without metrics/tracing
- Customize behavior

### 3. Lack of Contextual Information
Current implementation doesn't provide rich contextual information that would be valuable for debugging and monitoring.

### 4. Inconsistent Tagging Strategy
While tags are used consistently, there's no centralized definition of standard tags across the system.

### 5. Limited Extensibility
There's no clean way to add custom metrics or modify existing behavior without changing the core implementation.

### 6. Mixed Concerns
The current implementation mixes metrics collection, tracing, and business logic in the same service.

## Proposed Architecture

### 1. Domain-Driven Observability
Separate observability concerns into domain-specific components:
- Job-level observability
- Handler-level observability  
- Store-level observability
- Background service observability

### 2. Event-Driven Architecture
Introduce observability events that can be consumed by multiple handlers:
- `JobCreatedEvent`
- `JobProcessedEvent`
- `JobFailedEvent`
- `HandlerExecutedEvent`
- `StoreOperationEvent`

### 3. Pluggable Observability Providers
Create a provider model that allows different observability backends:
- Default provider (current implementation)
- OpenTelemetry provider
- Custom provider interface

### 4. Enhanced Contextual Information
Provide richer context for observability events including:
- Request/response details
- Performance metrics
- Error details
- Custom attributes

## New Interfaces and Abstractions

### Core Interfaces

```csharp
// Base observability provider interface
public interface IAsyncEndpointsObservabilityProvider
{
    void Initialize(IServiceProvider serviceProvider);
    void Shutdown();
}

// Job-specific observability
public interface IJobObservabilityProvider : IAsyncEndpointsObservabilityProvider
{
    void RecordJobCreated(JobCreatedEvent @event);
    void RecordJobProcessed(JobProcessedEvent @event);
    void RecordJobFailed(JobFailedEvent @event);
    void RecordJobRetried(JobRetriedEvent @event);
    void RecordJobQueueDuration(JobQueueDurationEvent @event);
    void RecordJobProcessingDuration(JobProcessingDurationEvent @event);
    void SetJobCurrentCount(JobCurrentCountEvent @event);
}

// Handler-specific observability
public interface IHandlerObservabilityProvider : IAsyncEndpointsObservabilityProvider
{
    void RecordHandlerExecution(HandlerExecutionEvent @event);
    void RecordHandlerError(HandlerErrorEvent @event);
}

// Store-specific observability
public interface IStoreObservabilityProvider : IAsyncEndpointsObservabilityProvider
{
    void RecordStoreOperation(StoreOperationEvent @event);
    void RecordStoreError(StoreErrorEvent @event);
}

// Background service observability
public interface IBackgroundServiceObservabilityProvider : IAsyncEndpointsObservabilityProvider
{
    void RecordBackgroundProcessing(BackgroundProcessingEvent @event);
}
```

### Event Classes

```csharp
public abstract class ObservabilityEvent
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object> Attributes { get; set; } = new();
    public Activity? Activity { get; set; }
}

public class JobCreatedEvent : ObservabilityEvent
{
    public Guid JobId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string StoreType { get; set; } = string.Empty;
    public Dictionary<string, List<string?>> Headers { get; set; } = new();
    public Dictionary<string, object?> RouteParams { get; set; } = new();
    public List<KeyValuePair<string, List<string?>>> QueryParams { get; set; } = new();
}

public class JobProcessedEvent : ObservabilityEvent
{
    public Guid JobId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public JobStatus Status { get; set; }
    public string StoreType { get; set; } = string.Empty;
    public Guid? WorkerId { get; set; }
    public TimeSpan ProcessingDuration { get; set; }
}

public class HandlerExecutionEvent : ObservabilityEvent
{
    public Guid JobId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string HandlerType { get; set; } = string.Empty;
    public TimeSpan ExecutionDuration { get; set; }
    public bool IsSuccessful { get; set; }
    public string? ResultType { get; set; }
}
```

### Observability Manager

```csharp
public interface IAsyncEndpointsObservabilityManager
{
    void Publish<TEvent>(TEvent @event) where TEvent : ObservabilityEvent;
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : ObservabilityEvent;
}

public class AsyncEndpointsObservabilityManager : IAsyncEndpointsObservabilityManager
{
    private readonly IEnumerable<IAsyncEndpointsObservabilityProvider> _providers;
    private readonly ILogger<AsyncEndpointsObservabilityManager> _logger;

    public AsyncEndpointsObservabilityManager(
        IEnumerable<IAsyncEndpointsObservabilityProvider> providers,
        ILogger<AsyncEndpointsObservabilityManager> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    public void Publish<TEvent>(TEvent @event) where TEvent : ObservabilityEvent
    {
        foreach (var provider in _providers)
        {
            try
            {
                ProcessEvent(provider, @event);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing observability event in provider {ProviderType}", provider.GetType().Name);
            }
        }
    }

    private static void ProcessEvent(IAsyncEndpointsObservabilityProvider provider, ObservabilityEvent @event)
    {
        // Use reflection or pattern matching to route events to appropriate provider methods
        // Implementation details would depend on specific event types
    }
}
```

### Default Implementation

```csharp
public class DefaultJobObservabilityProvider : IJobObservabilityProvider
{
    private readonly Meter _meter;
    private readonly Histogram<double> _jobProcessingDuration;
    private readonly Counter<long> _jobsCreated;
    private readonly Counter<long> _jobsProcessed;
    private readonly Counter<long> _jobsFailed;
    private readonly UpDownCounter<long> _jobsCurrentCount;
    private readonly ActivitySource _activitySource;

    public DefaultJobObservabilityProvider(IOptions<AsyncEndpointsConfigurations> configurations)
    {
        var config = configurations.Value.ObservabilityConfigurations;
        if (!config.EnableMetrics && !config.EnableTracing)
        {
            return;
        }

        _meter = new Meter("AsyncEndpoints", "1.0.0");
        _activitySource = new ActivitySource("AsyncEndpoints", "1.0.0");

        if (config.EnableMetrics)
        {
            _jobsCreated = _meter.CreateCounter<long>("asyncendpoints.jobs.created.total", 
                description: "Total number of jobs created");
            _jobsProcessed = _meter.CreateCounter<long>("asyncendpoints.jobs.processed.total", 
                description: "Total number of jobs processed");
            _jobsFailed = _meter.CreateCounter<long>("asyncendpoints.jobs.failed.total", 
                description: "Total number of job failures");
            _jobsCurrentCount = _meter.CreateUpDownCounter<long>("asyncendpoints.jobs.current.count", 
                description: "Current number of jobs in each state");
            _jobProcessingDuration = _meter.CreateHistogram<double>("asyncendpoints.jobs.processing.duration", 
                unit: "seconds", description: "Time spent processing jobs");
        }
    }

    public void RecordJobCreated(JobCreatedEvent @event)
    {
        if (_jobsCreated != null)
        {
            _jobsCreated.Add(1, GetTags(@event));
        }

        if (_activitySource.HasListeners())
        {
            var activity = _activitySource.StartActivity("Job.Submit", ActivityKind.Server);
            activity?.SetTag("job.id", @event.JobId.ToString());
            activity?.SetTag("job.name", @event.JobName);
            activity?.SetTag("store.type", @event.StoreType);
            activity?.Stop();
        }
    }

    private static KeyValuePair<string, object?>[] GetTags(ObservabilityEvent @event)
    {
        var tags = new List<KeyValuePair<string, object?>>();
        tags.Add(new("job.name", @event switch
        {
            JobCreatedEvent jce => jce.JobName,
            JobProcessedEvent jpe => jpe.JobName,
            _ => "unknown"
        }));

        // Add other common tags based on event type
        return tags.ToArray();
    }

    public void Initialize(IServiceProvider serviceProvider) { }
    public void Shutdown() 
    {
        _meter?.Dispose();
        _activitySource?.Dispose();
    }
}
```

## Implementation Strategy

### Phase 1: Core Infrastructure
1. Create the base event classes and interfaces
2. Implement the observability manager
3. Create the default provider implementations
4. Update dependency injection configuration

### Phase 2: Component Integration
1. Update JobManager to use observability events
2. Update JobProcessorService to use observability events
3. Update store implementations to use observability events
4. Update background services to use observability events

### Phase 3: Advanced Features
1. Implement OpenTelemetry provider
2. Add custom attribute support
3. Implement advanced filtering capabilities
4. Add performance optimizations

### Phase 4: Testing and Validation
1. Comprehensive unit testing
2. Integration testing
3. Performance testing
4. Documentation updates


## Testing Strategy

### Unit Tests
- Test each observability provider independently
- Test event routing and processing
- Test error handling in observability components

### Integration Tests
- Test end-to-end observability flow
- Test with different provider configurations
- Test performance impact of observability

### Performance Tests
- Measure overhead of observability events
- Test scalability under high load
- Validate that observability doesn't impact performance significantly

## Benefits of New Architecture

1. **Better Separation of Concerns**: Each provider handles specific observability aspects
2. **Enhanced Extensibility**: Easy to add new providers or modify existing behavior
3. **Improved Testability**: Components can be tested independently
4. **Richer Context**: Events carry more detailed information
5. **Better Performance**: Event-driven approach allows for optimizations
6. **Standards Compliance**: Better alignment with OpenTelemetry and other standards
7. **Maintainability**: Cleaner code structure and easier to modify

## Challenges

### 1. Performance Overhead
- Event-driven architecture may introduce additional overhead due to event publishing and processing
- Multiple providers processing the same events could impact performance under high load
- Need to implement efficient event routing and processing mechanisms

### 2. Complexity Management
- The new architecture introduces more interfaces and abstractions, potentially increasing complexity
- Developers need to understand the event-driven model and provider system
- More components to maintain and debug

### 3. Event Consistency and Ordering
- Ensuring events are processed in the correct order across different providers
- Handling scenarios where events might be lost or duplicated
- Maintaining consistency when multiple providers have different processing speeds

### 4. Error Handling and Resilience
- Managing failures in individual providers without affecting the entire system
- Ensuring observability doesn't become a single point of failure
- Implementing appropriate fallback mechanisms when providers fail

### 5. Memory Usage
- Storing and processing multiple event types may increase memory consumption
- Need to implement proper resource management and cleanup
- Potential for memory leaks if events or providers are not properly disposed

### 6. Configuration Complexity
- Managing multiple observability providers and their configurations
- Providing clear configuration options without overwhelming users
- Ensuring default configurations work well for most scenarios

### 7. Testing Complexity
- More complex testing scenarios due to multiple providers and event types
- Need to test various combinations of providers
- Performance testing becomes more critical due to potential overhead

### 8. Learning Curve
- Existing users will need to understand the new architecture
- Documentation and examples need to be comprehensive
- Migration from old patterns (if any existing code needs updating) may require effort

## Conclusion

After further analysis focusing on performance as the primary goal, we have developed a new high-performance observability architecture that prioritizes zero-overhead when disabled and minimal overhead when enabled. This approach ensures maximum performance for the core job processing functionality while maintaining comprehensive observability capabilities.

The new architecture is detailed in the separate document: [High-Performance Observability Architecture](./high-performance-observability-architecture.md), which should be used as the primary reference for implementation.