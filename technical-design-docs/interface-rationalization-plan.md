# Interface Rationalization Plan

**Document Status:** Draft  
**Created:** 2026-03-08  
**Author:** AsyncEndpoints Team  
**Reviewers:** TBD

---

## Executive Summary

This document outlines a comprehensive plan to reduce unnecessary interface abstractions in the AsyncEndpoints codebase. Through systematic analysis, we identified **14 interfaces** that were created solely for unit testing purposes, introducing maintenance overhead without providing genuine extensibility benefits.

### Goals

1. **Reduce Maintenance Overhead:** Eliminate redundant interface-implementation pairs
2. **Simplify DI Registration:** Reduce boilerplate in service registration
3. **Improve Code Clarity:** Make the codebase easier to navigate and understand
4. **Preserve Genuine Abstractions:** Keep interfaces where multiple implementations exist or are anticipated

### Non-Goals

1. Removing interfaces that enable genuine polymorphism (e.g., `IJobStore`)
2. Breaking public APIs used by framework consumers
3. Compromising testability - we will use alternative testing strategies

---

## Current State Analysis

### Solution Structure

```
AsyncEndpoints.sln
├── src/
│   ├── AsyncEndpoints/           # Core library
│   └── AsyncEndpoints.Redis/     # Redis implementation
├── tests/
│   ├── AsyncEndpoints.UnitTests/
│   └── AsyncEndpoints.Redis.UnitTests/
└── technical-design-docs/
```

### Identified Interfaces

| Category | Count | Decision |
|----------|-------|----------|
| Keep (Genuine Abstractions) | 4 | Retain |
| Remove (Test-Only Overhead) | 14 | Eliminate |
| Special Case (User-Facing) | 1 | Retain |

---

## Interfaces to Keep

These interfaces represent genuine abstraction points with multiple implementations or clear extensibility requirements.

### 1. `IJobStore`

**Location:** `src/AsyncEndpoints/JobProcessing/IJobStore.cs`

**Implementations:**
- `InMemoryJobStore` - Development/single-instance deployments
- `RedisJobStore` - Production/distributed deployments

**Rationale:** Core storage abstraction enabling different persistence strategies. This is the primary extension point for the framework.

**Methods:**
```csharp
Task<MethodResult> CreateJob(Job job, CancellationToken cancellationToken);
Task<MethodResult<Job>> GetJobById(Guid id, CancellationToken cancellationToken);
Task<MethodResult> UpdateJob(Job job, CancellationToken cancellationToken);
Task<MethodResult<Job>> ClaimNextJobForWorker(Guid workerId, CancellationToken cancellationToken);
bool SupportsJobRecovery { get; }
Task<int> RecoverStuckJobs(long timeoutUnixTime, int maxRetries, CancellationToken cancellationToken);
```

---

### 2. `IJobRecoveryService`

**Location:** `src/AsyncEndpoints/JobProcessing/IJobRecoveryService.cs`

**Implementations:**
- `InMemoryJobRecoveryService` - No-op implementation
- `RedisJobRecoveryService` - Lua script-based recovery

**Rationale:** Tied to `IJobStore` - different recovery strategies based on storage backend capabilities.

---

### 3. `IRedisLuaScriptService`

**Location:** `src/AsyncEndpoints.Redis/Services/IRedisLuaScriptService.cs`

**Implementations:**
- `RedisLuaScriptService`

**Rationale:** Redis-specific abstraction. Worth keeping for:
- Testing Redis logic without actual Redis connection
- Potential future optimization of Lua scripts
- Clear separation of Redis operations from storage logic

---

### 4. `IJobHashConverter`

**Location:** `src/AsyncEndpoints.Redis/Services/IJobHashConverter.cs`

**Implementations:**
- `JobHashConverter`

**Rationale:** Redis-specific serialization contract. Keep for:
- Testability of Redis conversion logic
- Potential future support for different serialization formats

---

### 5. `IAsyncEndpointRequestHandler<TRequest, TResponse>` (Special Case)

**Location:** `src/AsyncEndpoints/Handlers/IAsyncEndpointRequestHandler.cs`

**Implementations:** User-provided handlers

**Rationale:** **User-facing contract** for registering business logic. This is how framework consumers implement their async endpoint handlers. **Not a candidate for removal.**

---

## Interfaces to Remove

### Phase 1: Background Worker Services (Quick Wins)

These services orchestrate internal background processing with no realistic alternative implementations.

#### 1.1 `IDelayCalculatorService` ↔ `DelayCalculatorService`

**Location:** `src/AsyncEndpoints/Background/`

**Current Usage:**
```csharp
// DelayCalculatorService.cs
public class DelayCalculatorService : IDelayCalculatorService
{
    public TimeSpan CalculateDelay(JobClaimingState state, AsyncEndpointsWorkerConfigurations workerConfigurations)
    {
        return state switch { ... };
    }
}
```

**Why Remove:**
- Simple switch-based calculation with no variation points
- No realistic scenario for alternative implementations
- Logic is purely internal to the framework

**Files to Modify:**
- `src/AsyncEndpoints/Background/DelayCalculatorService.cs` - Remove interface
- `src/AsyncEndpoints/Background/JobProducerService.cs` - Update injection
- `src/AsyncEndpoints/Extensions/ServiceCollectionExtensions.cs` - Update registration
- `tests/AsyncEndpoints.UnitTests/Background/DelayCalculatorServiceTests.cs` - Update tests

---

#### 1.2 `IJobChannelEnqueuer` ↔ `JobChannelEnqueuer`

**Location:** `src/AsyncEndpoints/Background/`

**Current Usage:**
```csharp
public class JobChannelEnqueuer : IJobChannelEnqueuer
{
    public async Task<bool> Enqueue(ChannelWriter<Job> writerJobChannel, Job job, CancellationToken stoppingToken)
    {
        // Channel write logic with timeout
    }
}
```

**Why Remove:**
- Single responsibility with no variation points
- Implementation detail of channel communication
- No external extensibility requirement

**Files to Modify:**
- `src/AsyncEndpoints/Background/JobChannelEnqueuer.cs` - Remove interface
- `src/AsyncEndpoints/Background/JobClaimingService.cs` - Update injection
- `src/AsyncEndpoints/Extensions/ServiceCollectionExtensions.cs` - Update registration
- `tests/AsyncEndpoints.UnitTests/Background/JobChannelEnqueuerTests.cs` - Update tests

---

#### 1.3 `IJobClaimingService` ↔ `JobClaimingService`

**Location:** `src/AsyncEndpoints/Background/`

**Current Usage:**
```csharp
public class JobClaimingService : IJobClaimingService
{
    public async Task<JobClaimingState> ClaimAndEnqueueJobAsync(...)
    {
        // Orchestrates claiming and enqueueing
    }
}
```

**Why Remove:**
- Internal orchestration logic
- No anticipated alternative implementations
- Specific to the framework's internal architecture

**Files to Modify:**
- `src/AsyncEndpoints/Background/JobClaimingService.cs` - Remove interface
- `src/AsyncEndpoints/Background/JobProducerService.cs` - Update injection
- `src/AsyncEndpoints/Extensions/ServiceCollectionExtensions.cs` - Update registration
- `tests/AsyncEndpoints.UnitTests/Background/JobClaimingServiceTests.cs` - Update tests

---

### Phase 2: Background Processing Pipeline

#### 2.1 `IJobProducerService` ↔ `JobProducerService`

**Location:** `src/AsyncEndpoints/Background/`

**Why Remove:**
- Background polling implementation detail
- Adaptive polling logic is internal
- No extensibility requirement

**Files to Modify:**
- `src/AsyncEndpoints/Background/JobProducerService.cs` - Remove interface
- `src/AsyncEndpoints/Background/AsyncEndpointsBackgroundService.cs` - Update injection
- `src/AsyncEndpoints/Extensions/ServiceCollectionExtensions.cs` - Update registration
- `tests/AsyncEndpoints.UnitTests/Background/JobProducerServiceTests.cs` - Update tests

---

#### 2.2 `IJobConsumerService` ↔ `JobConsumerService`

**Location:** `src/AsyncEndpoints/Background/`

**Why Remove:**
- Channel consumption loop implementation
- No realistic alternative implementations
- Internal processing detail

**Files to Modify:**
- `src/AsyncEndpoints/Background/JobConsumerService.cs` - Remove interface
- `src/AsyncEndpoints/Background/AsyncEndpointsBackgroundService.cs` - Update injection
- `src/AsyncEndpoints/Extensions/ServiceCollectionExtensions.cs` - Update registration
- `tests/AsyncEndpoints.UnitTests/Background/JobConsumerServiceTests.cs` - Update tests

---

#### 2.3 `IJobProcessorService` ↔ `JobProcessorService`

**Location:** `src/AsyncEndpoints/Background/`

**Why Remove:**
- Single job processing orchestration
- Internal workflow with no variation points
- No extensibility requirement

**Files to Modify:**
- `src/AsyncEndpoints/Background/JobProcessorService.cs` - Remove interface
- `src/AsyncEndpoints/Background/JobConsumerService.cs` - Update injection
- `src/AsyncEndpoints/Extensions/ServiceCollectionExtensions.cs` - Update registration
- `tests/AsyncEndpoints.UnitTests/Background/JobProcessorServiceTests.cs` - Update tests

---

#### 2.4 `IHandlerExecutionService` ↔ `HandlerExecutionService`

**Location:** `src/AsyncEndpoints/Background/`

**Why Remove:**
- Service scope creation for handler execution
- Implementation detail of the framework
- No anticipated alternatives

**Files to Modify:**
- `src/AsyncEndpoints/Background/HandlerExecutionService.cs` - Remove interface
- `src/AsyncEndpoints/Background/JobProcessorService.cs` - Update injection
- `src/AsyncEndpoints/Extensions/ServiceCollectionExtensions.cs` - Update registration
- `tests/AsyncEndpoints.UnitTests/Background/HandlerExecutionServiceTests.cs` - Update tests

---

### Phase 3: Infrastructure Services

#### 3.1 `IDateTimeProvider` ↔ `DateTimeProvider`

**Location:** `src/AsyncEndpoints/Infrastructure/`

**Current Usage:** Used across 7+ services for testable time access

**Why Remove:**
- .NET 8+ provides built-in `TimeProvider`
- Direct `DateTime.UtcNow` usage is acceptable for most cases
- Test mocking can use alternative strategies

**Migration Strategy:**
```csharp
// Before
public class MyService(IDateTimeProvider dateTimeProvider) { ... }

// After (Option A: Direct usage)
public class MyService { ... DateTime.UtcNow ... }

// After (Option B: TimeProvider for testability)
public class MyService(TimeProvider timeProvider = null) 
{ 
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
}
```

**Files to Modify:**
- `src/AsyncEndpoints/Infrastructure/DateTimeProvider.cs` - Remove interface
- `src/AsyncEndpoints/JobProcessing/JobManager.cs` - Update usage
- `src/AsyncEndpoints/JobProcessing/InMemoryJobStore.cs` - Update usage
- `src/AsyncEndpoints/Background/JobProcessorService.cs` - Update usage
- `src/AsyncEndpoints/Redis/Storage/RedisJobStore.cs` - Update usage
- `src/AsyncEndpoints/Redis/Services/RedisLuaScriptService.cs` - Update usage
- `src/AsyncEndpoints/Extensions/ServiceCollectionExtensions.cs` - Update registration
- All test files mocking this interface

---

#### 3.2 `ISerializer` ↔ `Serializer`

**Location:** `src/AsyncEndpoints/Infrastructure/Serialization/`

**Current Usage:** Wrapper around System.Text.Json

**Why Remove:**
- Single implementation wrapping static methods
- No realistic alternative serialization strategy
- Adds indirection without benefit

**Migration Strategy:**
```csharp
// Before
public class MyService(ISerializer serializer) { ... }

// After (Direct JsonSerializer usage)
using System.Text.Json;
public class MyService { ... JsonSerializer.Serialize(...) ... }
```

**Files to Modify:**
- `src/AsyncEndpoints/Infrastructure/Serialization/Serializer.cs` - Remove interface
- `src/AsyncEndpoints/Background/JobProcessorService.cs` - Update usage
- `src/AsyncEndpoints/Handlers/AsyncEndpointRequestDelegate.cs` - Update usage
- `src/AsyncEndpoints/Redis/Storage/RedisJobStore.cs` - Update usage
- `src/AsyncEndpoints/Redis/Services/JobHashConverter.cs` - Update usage
- `src/AsyncEndpoints/Extensions/ServiceCollectionExtensions.cs` - Update registration
- All test files mocking this interface

---

#### 3.3 `IJsonBodyParserService` ↔ `JsonBodyParserService`

**Location:** `src/AsyncEndpoints/Infrastructure/Serialization/`

**Why Remove:**
- Single implementation with no variation points
- Could be simplified to extension method on `HttpContext`

**Migration Strategy:**
```csharp
// Before
var result = await _jsonBodyParserService.ParseAsync<T>(httpContext, cancellationToken);

// After (Extension method)
var result = await httpContext.ReadJsonAsync<T>(cancellationToken);
```

**Files to Modify:**
- `src/AsyncEndpoints/Infrastructure/Serialization/JsonBodyParserService.cs` - Remove interface
- `src/AsyncEndpoints/Extensions/ServiceCollectionExtensions.cs` - Update registration
- `tests/AsyncEndpoints.UnitTests/Infrastructure/JsonBodyParserServiceTests.cs` - Update tests

---

#### 3.4 `IAsyncEndpointsObservability` ↔ `AsyncEndpointsObservability`

**Location:** `src/AsyncEndpoints/Infrastructure/Observability/`

**Current Usage:** Used across 10+ services for metrics and tracing

**Why Remove:**
- Single implementation
- Null object pattern can handle disabled state
- Conditional logic simpler than abstraction

**Migration Strategy:**
```csharp
// Before
public class MyService(IAsyncEndpointsObservability metrics) { ... }

// After (Direct usage with conditional logic)
public class MyService(AsyncEndpointsObservability metrics) { ... }
// Or use null object pattern internally
```

**Files to Modify:**
- `src/AsyncEndpoints/Infrastructure/Observability/AsyncEndpointsObservability.cs` - Remove interface
- All services injecting this interface (10+ files)
- `src/AsyncEndpoints/Extensions/ServiceCollectionExtensions.cs` - Update registration
- All test files mocking this interface

---

### Phase 4: Core Services

#### 4.1 `IJobManager` ↔ `JobManager`

**Location:** `src/AsyncEndpoints/JobProcessing/`

**Current Usage:** Core job lifecycle management

**Why Remove:**
- Currently single implementation
- Internal framework orchestration
- No anticipated alternative implementations

**Files to Modify:**
- `src/AsyncEndpoints/JobProcessing/JobManager.cs` - Remove interface
- `src/AsyncEndpoints/Background/JobClaimingService.cs` - Update injection
- `src/AsyncEndpoints/Background/JobProcessorService.cs` - Update injection
- `src/AsyncEndpoints/Handlers/AsyncEndpointRequestDelegate.cs` - Update injection
- `src/AsyncEndpoints/Extensions/ServiceCollectionExtensions.cs` - Update registration
- `tests/AsyncEndpoints.UnitTests/JobProcessing/JobManagerTests.cs` - Update tests
- `tests/AsyncEndpoints.UnitTests/JobProcessing/JobManagerObservabilityTests.cs` - Update tests

---

#### 4.2 `IAsyncEndpointRequestDelegate` ↔ `AsyncEndpointRequestDelegate`

**Location:** `src/AsyncEndpoints/Handlers/`

**Why Remove:**
- Framework's public API implementation
- No realistic alternative implementation
- Simplifies route builder extensions

**Files to Modify:**
- `src/AsyncEndpoints/Handlers/AsyncEndpointRequestDelegate.cs` - Remove interface
- `src/AsyncEndpoints/Extensions/RouteBuilderExtensions.cs` - Update usage
- `src/AsyncEndpoints/Extensions/ServiceCollectionExtensions.cs` - Update registration
- `tests/AsyncEndpoints.UnitTests/Handlers/AsyncEndpointRequestDelegateTests.cs` - Update tests
- `tests/AsyncEndpoints.UnitTests/Handlers/AsyncEndpointRequestDelegateExceptionTests.cs` - Update tests

---

## Implementation Strategy

### Pre-Refactoring Checklist

- [ ] Ensure all tests are passing
- [ ] Create backup branch
- [ ] Document current DI registration patterns
- [ ] Identify all interface usage locations

### Phase Execution Order

```
Phase 1 (Quick Wins)
├── 1.1 IDelayCalculatorService
├── 1.2 IJobChannelEnqueuer
└── 1.3 IJobClaimingService

Phase 2 (Background Pipeline)
├── 2.1 IJobProducerService
├── 2.2 IJobConsumerService
├── 2.3 IJobProcessorService
└── 2.4 IHandlerExecutionService

Phase 3 (Infrastructure)
├── 3.1 IDateTimeProvider
├── 3.2 ISerializer
├── 3.3 IJsonBodyParserService
└── 3.4 IAsyncEndpointsObservability

Phase 4 (Core)
├── 4.1 IJobManager
└── 4.2 IAsyncEndpointRequestDelegate
```

### Per-Phase Steps

1. **Remove interface file**
2. **Update implementation** (remove `: IInterface`)
3. **Update all injection points** (change interface to concrete type)
4. **Update DI registration** (remove interface registration)
5. **Update tests** (mock concrete class or use real instance)
6. **Run tests** (verify no regressions)
7. **Build solution** (verify compilation)

---

## Testing Strategy

### Unit Tests

For removed interfaces, update tests to:

1. **Use concrete class directly** when testing internal logic
2. **Mock concrete dependencies** instead of interface dependencies
3. **Use real instances** for simple services (e.g., `DelayCalculatorService`)

### Example Transformation

**Before:**
```csharp
public class JobProducerServiceTests
{
    private readonly Mock<IJobClaimingService> _mockJobClaimingService;
    private readonly Mock<IDelayCalculatorService> _mockDelayCalculator;
    private JobProducerService _service;

    [SetUp]
    public void SetUp()
    {
        _mockJobClaimingService = new Mock<IJobClaimingService>();
        _mockDelayCalculator = new Mock<IDelayCalculatorService>();
        _service = new JobProducerService(..., _mockDelayCalculator.Object);
    }
}
```

**After:**
```csharp
public class JobProducerServiceTests
{
    private readonly Mock<JobClaimingService> _mockJobClaimingService;
    private readonly DelayCalculatorService _delayCalculator;
    private JobProducerService _service;

    [SetUp]
    public void SetUp()
    {
        _mockJobClaimingService = new Mock<JobClaimingService>();
        _delayCalculator = new DelayCalculatorService(...); // Use real instance
        _service = new JobProducerService(..., _delayCalculator);
    }
}
```

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Breaking public API | Low | High | Review all public-facing types; keep user-facing interfaces |
| Test coverage gaps | Medium | Medium | Ensure tests pass after each phase; add integration tests |
| Future extensibility needs | Low | Medium | Document rationale; interfaces can be reintroduced if needed |
| Merge conflicts | Medium | Low | Complete phases sequentially; communicate with team |

---

## Success Metrics

1. **Reduced File Count:** ~28 fewer files (14 interfaces + 14 test files)
2. **Simplified DI:** ~12 fewer service registrations
3. **Improved Navigation:** Fewer files to search through
4. **Maintained Test Coverage:** All tests passing with equivalent coverage
5. **Zero Breaking Changes:** Public API remains stable for framework consumers

---

## Appendix: Files Inventory

### Interface Files to Delete (14)

```
src/AsyncEndpoints/Background/IDelayCalculatorService.cs
src/AsyncEndpoints/Background/IJobChannelEnqueuer.cs
src/AsyncEndpoints/Background/IJobClaimingService.cs
src/AsyncEndpoints/Background/IJobProducerService.cs
src/AsyncEndpoints/Background/IJobConsumerService.cs
src/AsyncEndpoints/Background/IJobProcessorService.cs
src/AsyncEndpoints/Background/IHandlerExecutionService.cs
src/AsyncEndpoints/Infrastructure/IDateTimeProvider.cs
src/AsyncEndpoints/Infrastructure/Serialization/ISerializer.cs
src/AsyncEndpoints/Infrastructure/Serialization/IJsonBodyParserService.cs
src/AsyncEndpoints/Infrastructure/Observability/IAsyncEndpointsObservability.cs
src/AsyncEndpoints/JobProcessing/IJobManager.cs
src/AsyncEndpoints/Handlers/IAsyncEndpointRequestDelegate.cs
```

### Implementation Files to Modify (~20)

All corresponding `.cs` files will be updated to remove interface implementation and update dependencies.

### Test Files to Update (~20)

All corresponding unit test files mocking these interfaces will need updates.

---

## References

- [YAGNI Principle](https://martinfowler.com/bliki/Yagni.html)
- [Interface Segregation Principle](https://en.wikipedia.org/wiki/Interface_segregation_principle)
- [.NET TimeProvider Documentation](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider)
