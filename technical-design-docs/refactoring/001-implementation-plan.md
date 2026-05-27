# Comprehensive Implementation Plan: Architecture Realignment

This document maps the complete transition from the current `AsyncEndpoints` codebase to the new architecture defined in `001-initial-design.md`, `002-folder-structure.md`, and `003-channels-and-partitions.md`. It exhaustively covers every file, every challenge, and provides a phased execution strategy.

---

## Table of Contents
1. [Current State Analysis](#1-current-state-analysis)
2. [Complete File Mapping: Old → New](#2-complete-file-mapping-old--new)
3. [Architecture Challenges (Categorized)](#3-architecture-challenges-categorized)
4. [New Files to Create](#4-new-files-to-create)
5. [Files to Delete](#5-files-to-delete)
6. [Phased Execution Plan](#6-phased-execution-plan)
7. [Backward Compatibility Strategy](#7-backward-compatibility-strategy)
8. [Testing Strategy](#8-testing-strategy)
9. [Risk Register](#9-risk-register)
10. [Definition of Done](#10-definition-of-done)

---

## 1. Current State Analysis

### 1.1 Project Structure (Current)

```
src/
├── AsyncEndpoints.Abstractions/          # Has ASP.NET dependency - violates zero-dep rule
│   ├── Infrastructure/                   #   IDateTimeProvider, DateTimeProvider
│   ├── Utilities/                        #   AsyncEndpointError, ExceptionInfo, MethodResult
│   └── AsyncEndpoints.Abstractions.csproj # References AspNetCore.Http.Abstractions
│
├── AsyncEndpoints.Core/                  # Core logic - deeply coupled to ASP.NET
│   ├── Background/                       #   Interfaces for producer/consumer pipeline
│   ├── Configuration/                    #   6 config classes (some reference IResult/HttpContext)
│   ├── Handlers/                         #   AsyncContext, IAsyncEndpointRequestHandler
│   ├── Infrastructure/                   #   Serialization, JSON context, Observability
│   ├── JobProcessing/                    #   Job, IJobStore, IJobManager, JobManager, JobStatus
│   ├── Utilities/                        #   Response defaults, JobResponse, JobResultResponse
│   ├── HttpContextExtensions.cs          #   ❌ Core has HttpContext concern
│   └── AsyncEndpoints.Core.csproj        # Has FrameworkReference AspNetCore.App
│
├── AsyncEndpoints.AspNetCore/            # ASP.NET integration layer
│   ├── Extensions/                       #   RouteBuilderExtensions, ServiceCollectionExtensions
│   ├── Handlers/                         #   AsyncEndpointRequestDelegate, IAsyncEndpointRequestDelegate
│   └── AsyncEndpoints.AspNetCore.csproj  # References Core only
│
├── AsyncEndpoints.Worker/                # Background worker
│   ├── Background/                       #   10 files: producer, consumer, channel, processor, etc.
│   └── AsyncEndpoints.Worker.csproj      # References Core only
│
├── AsyncEndpoints/                       # Aggregator/meta package
│   ├── Extensions/ServiceCollectionExtensions.cs  # Duplicates AspNetCore's DI registrations
│   └── AsyncEndpoints.csproj             # Aggregates Core+Worker+AspNetCore+InMemory
│
├── AsyncEndpoints.Provider.InMemory/     # In-memory provider
│   ├── JobProcessing/                    #   InMemoryJobStore, InMemoryJobRecoveryService
│   └── AsyncEndpoints.Provider.InMemory.csproj  # References Core (should reference Abstractions only)
│
└── AsyncEndpoints.Redis/                 # Redis provider
    ├── Configuration/                    #   RedisConfiguration
    ├── Extensions/                       #   ServiceCollectionExtensions
    ├── Services/                         #   Lua scripts, hash converter, recovery service
    ├── Storage/                          #   RedisJobStore
    └── AsyncEndpoints.Redis.csproj       # References AsyncEndpoints aggregator (wrong)
```

### 1.2 Dependency Graph (Current — with violations)

```
AspNetCore ──► Core ──► Abstractions
Worker ──────► Core ──► Abstractions
InMemory ────► Core ──► Abstractions     ❌ Should point to Abstractions only
Redis ───────► AsyncEndpoints (aggregator) ❌ Wrong reference
               ├────► Core ──► Abstractions
               ├────► Worker
               ├────► AspNetCore
               └────► InMemory
Abstractions ─► Microsoft.AspNetCore.Http.Abstractions  ❌ Should have zero dependencies
Core ─────────► Microsoft.AspNetCore.App                 ❌ Should not need ASP.NET
```

### 1.3 Dependency Graph (Target)

```
AspNetCore ──► Core ──► Abstractions    (zero deps)
Worker ──────► Core ──► Abstractions
Providers ─────────────► Abstractions   (optionally Core for helpers)
```

---

## 2. Complete File Mapping: Old → New

### 2.1 `AsyncEndpoints.Abstractions` (Old → New)

| Old File | Action | New File | Notes |
|----------|--------|----------|-------|
| `Infrastructure/IDateTimeProvider.cs` | **Move + Refactor** → `Abstractions/Common/` | Rename to remove `I` prefix if needed, or keep as `ITimeProvider` | Consider if this is the right abstraction vs. just using `TimeProvider` from .NET |
| `Infrastructure/DateTimeProvider.cs` | **Move** → `Core/Internal/` | Keep as implementation detail in Core | Not a public contract |
| `Utilities/AsyncEndpointError.cs` | **Keep + Rename** → `Abstractions/Common/JobError.cs` | Rename to avoid confusion with new design | Current name generic but fine; evaluate if still needed |
| `Utilities/ExceptionInfo.cs` | **Keep** → `Abstractions/Common/` | Keep | Debugging serialization |
| `Utilities/InnerExceptionInfo.cs` | **Keep** → `Abstractions/Common/` | Keep | Debugging serialization |
| `Utilities/MethodResult.cs` | **Refactor** → `Abstractions/Common/Result.cs` | Simplify: remove HTTP-specific error fields. Align with `Result<T>` pattern used in design doc | Current impl has awkward `DataOrNull` pattern |
| `AsyncEndpoints.Abstractions.csproj` | **Fix** | Remove `Microsoft.AspNetCore.Http.Abstractions` dependency | **CRITICAL**: Must have zero dependencies |

### 2.2 `AsyncEndpoints.Core` (Old → New)

| Old File | Action | New File | Notes |
|----------|--------|----------|-------|
| `JobProcessing/Job.cs` | **SPLIT** → `Abstractions/Jobs/JobRecord.cs` + `Abstractions/Jobs/JobDescriptor.cs` + Core/Internal | Remove HTTP properties, state machine logic. `JobRecord` = pure POCO, `JobDescriptor` = submission input | **Major challenge** |
| `JobProcessing/JobStatus.cs` | **Move** → `Abstractions/Jobs/JobStatus.cs` | Add `DeadLettered` status (missing) | Design doc specifies this state |
| `JobProcessing/IJobStore.cs` | **REWRITE** → `Abstractions/Storage/IJobStore.cs` | New contract: `EnqueueAsync`, `DequeueAsync`, `UpdateStatusAsync`, `GetStatusAsync`, `HeartbeatAsync`, `ReclaimStaleJobsAsync` | **Major challenge** |
| `JobProcessing/IJobManager.cs` | **DELETE** | Replaced by `IJobSubmitter` + `IJobStore` directly | Orchestration layer in new design is thinner |
| `JobProcessing/JobManager.cs` | **REWRITE** → `Core/Submission/JobSubmitter.cs` | Remove `HttpContext`. Accept `JobDescriptor`. Implement `IJobSubmitter` | |
| `JobProcessing/IJobRecoveryService.cs` | **DELETE** | Heartbeat + reclaim moved into `IJobStore` per design | |
| `JobProcessing/ErrorType.cs` | **DELETE** | Not in design doc. Retry handling moved to `RetryHandler` in Worker | |
| `Handlers/IAsyncEndpointRequestHandler.cs` | **DELETE** → replaced by `Abstractions/Jobs/IJobHandler.cs` | New: `Task HandleAsync(TJob job, CancellationToken ct)` | **Major change for consumers** |
| `Handlers/AsyncContext.cs` | **DELETE** (from Core) → move HTTP portion to `AspNetCore/Models/` | HTTP context data not part of core job processing | |
| `Handlers/HandlerRegistration.cs` | **REWRITE** → `Core/Execution/IHandlerRegistry.cs` + `HandlerRegistry.cs` | Non-static, DI-registered registry of typed delegates. Stores `Func<IServiceProvider, JobRecord, CancellationToken, Task>` keyed by job name. Registering `IJobHandler<T>` via `AddJobHandler<T>()` stores a delegate that captures `T` at compile time — no reflection. | |
| `Handlers/NoBodyRequest.cs` | **DELETE** | No-body concept gone; `IJobHandler<T>` always receives `T` | |
| `Background/IHandlerExecutionService.cs` | **DELETE** | Handler execution moves to `Core/Execution/JobDispatcher.cs` | |
| `Background/IJobChannelEnqueuer.cs` | **DELETE** | Channel-based pipeline removed | |
| `Background/IJobClaimingService.cs` | **DELETE** | Claiming logic moves to `IJobStore.DequeueAsync` | |
| `Background/IJobConsumerService.cs` | **DELETE** | Consumer pattern removed | |
| `Background/IJobProcessorService.cs` | **DELETE** | Processing moves to `JobExecutionPipeline` in Worker | |
| `Background/IJobProducerService.cs` | **DELETE** | Producer pattern removed | |
| `Background/JobClaimingState.cs` | **DELETE** | Enum no longer needed | |
| `Configuration/AsyncEndpointsConfigurations.cs` | **REWRITE** → `Core/Configuration/AsyncEndpointsOptions.cs` | Align with `AsyncEndpointsOptions` in design doc | |
| `Configuration/AsyncEndpointsConstants.cs` | **REFACTOR** → `Core/Configuration/` | Keep internal constants, remove HTTP-specific ones | |
| `Configuration/AsyncEndpointsJobManagerConfigurations.cs` | **REWRITE** → `Core/Configuration/` | Fold into `AsyncEndpointsOptions` or `WorkerOptions` | |
| `Configuration/AsyncEndpointsObservabilityConfigurations.cs` | **MOVE** → `Core/Configuration/` | Keep | |
| `Configuration/AsyncEndpointsRecoveryConfigurations.cs` | **REWRITE** → `Worker/Hosting/WorkerOptions.cs` | Stale job timeout, heartbeat interval move to options | |
| `Configuration/AsyncEndpointsResponseConfigurations.cs` | **MOVE** → `AspNetCore/Configuration/` | Contains `HttpContext`/`IResult` — belongs in AspNetCore | |
| `Configuration/AsyncEndpointsWorkerConfigurations.cs` | **REWRITE** → `Worker/Hosting/WorkerOptions.cs` | Concurrency, polling intervals | |
| `Infrastructure/AsyncEndpointsJsonSerializationContext.cs` | **REWRITE** → `Core/Serialization/` | Strip Job class ref, add JobRecord/JobDescriptor | |
| `Infrastructure/Observability/AsyncEndpointsObservability.cs` | **KEEP + REFACTOR** → `Core/Observability/` | Update for new data model. No ASP.NET deps needed | |
| `Infrastructure/Observability/IAsyncEndpointsObservability.cs` | **KEEP + REFACTOR** → `Core/Observability/` | Same | |
| `Infrastructure/Observability/MetricTimer.cs` | **KEEP** → `Core/Observability/` | Utility | |
| `Infrastructure/Serialization/IJsonBodyParserService.cs` | **MOVE** → `AspNetCore/Serialization/` | Contains `HttpContext` reference | |
| `Infrastructure/Serialization/ISerializer.cs` | **KEEP** → `Core/Serialization/` | No ASP.NET deps | |
| `Infrastructure/Serialization/JsonBodyParserService.cs` | **MOVE** → `AspNetCore/Serialization/` | Contains `HttpContext` reference | |
| `Infrastructure/Serialization/Serializer.cs` | **REFACTOR** → `Core/Serialization/JobSerializer.cs` | Remove dependency on `Microsoft.AspNetCore.Http.Json.JsonOptions` | |
| `Utilities/AsyncContextBuilder.cs` | **DELETE** → replaced by `JobDispatcher` | | |
| `Utilities/HandlerRegistrationTracker.cs` | **RENAME + REFACTOR** → `Core/Execution/IHandlerRegistry.cs` + `HandlerRegistry.cs` | Static global replaced by DI-registered service. Delegate-registry pattern preserved (required for AOT safety). | |
| `Utilities/JobResponse.cs` | **MOVE** → `AspNetCore/Models/JobResponse.cs` | HTTP response DTO | |
| `Utilities/JobResponseMapper.cs` | **MOVE** → `AspNetCore/Models/` | Mapping logic for HTTP responses | |
| `Utilities/JobResultResponse.cs` | **MOVE** → `AspNetCore/Endpoints/` | Implements `IResult` | |
| `Utilities/NullDisposable.cs` | **KEEP** → `Core/Internal/` | Internal utility | |
| `Utilities/ResponseDefaults.cs` | **MOVE** → `AspNetCore/Endpoints/` | References `IResult` | |
| `HttpContextExtensions.cs` | **MOVE** → `AspNetCore/Extensions/HttpContextExtensions.cs` | Belongs in AspNetCore | |

### 2.3 `AsyncEndpoints.AspNetCore` (Old → New)

| Old File | Action | New File | Notes |
|----------|--------|----------|-------|
| `Extensions/RouteBuilderExtensions.cs` | **REFACTOR** → `AspNetCore/Endpoints/JobEndpoints.cs` + `JobStatusEndpoint.cs` | Use `IJobSubmitter` instead of `IJobManager`. Remove handler-injection parameter | |
| `Extensions/ServiceCollectionExtensions.cs` | **SPLIT**: Worker registration → `Worker/`, Core registration → `Core/` | Keep only AspNetCore-specific registrations | |
| `Handlers/AsyncEndpointRequestDelegate.cs` | **REWRITE** → `AspNetCore/Endpoints/JobSubmissionEndpoint.cs` | Simplify: parse body → create descriptor → submit → return | |
| `Handlers/IAsyncEndpointRequestDelegate.cs` | **DELETE** | No longer needed | |
| New | `AspNetCore/Models/JobResponse.cs` | Response DTOs | |
| New | `AspNetCore/Extensions/HttpContextExtensions.cs` | Moved from Core | |

### 2.4 `AsyncEndpoints.Worker` (Old → New)

| Old File | Action | New File | Notes |
|----------|--------|----------|-------|
| `Background/AsyncEndpointsBackgroundService.cs` | **REWRITE** → `Worker/Hosting/JobWorkerService.cs` | Use `PollingJobListener`, one per channel. No Channel<T> | |
| `Background/JobProducerService.cs` | **DELETE** | Replaced by `PollingJobListener` | |
| `Background/JobConsumerService.cs` | **DELETE** | Replaced by `JobWorkerService` | |
| `Background/JobClaimingService.cs` | **DELETE** | | |
| `Background/JobChannelEnqueuer.cs` | **DELETE** | | |
| `Background/JobProcessorService.cs` | **DELETE** → functionality moves to `Worker/Execution/JobExecutionPipeline.cs` | | |
| `Background/HandlerExecutionService.cs` | **REWRITE** → `Worker/Execution/JobExecutionPipeline.cs` | + `RetryHandler.cs` | |
| `Background/DelayCalculatorService.cs` | **DELETE** | Adaptive polling built into `PollingJobListener` | |
| `Background/IDelayCalculatorService.cs` | **DELETE** | | |
| `Background/IJobProducerService.cs` | **DELETE** | | |
| `Background/IJobConsumerService.cs` | **DELETE** | | |
| `Background/IJobClaimingService.cs` | **DELETE** | | |
| `Background/IJobChannelEnqueuer.cs` | **DELETE** | | |
| `Background/IJobProcessorService.cs` | **DELETE** | | |
| `Background/IHandlerExecutionService.cs` | **DELETE** | | |
| `Background/DistributedJobRecoveryService.cs` | **REWRITE** → `Worker/Heartbeat/HeartbeatService.cs` + `Worker/Hosting/StaleJobSweeper.cs` | Heartbeat per active job + sweeper for stale jobs | |
| New | `Worker/Concurrency/WorkerConcurrencyManager.cs` | SemaphoreSlim per channel/worker | |
| New | `Worker/Hosting/WorkerOptions.cs` | Polling intervals, concurrency, heartbeat config | |

### 2.5 `AsyncEndpoints.Provider.InMemory` (Old → New)

| Old File | Action | New File | Notes |
|----------|--------|----------|-------|
| `JobProcessing/InMemoryJobStore.cs` | **REWRITE** → `InMemory/Storage/InMemoryJobStore.cs` | Implement new `IJobStore`: `EnqueueAsync`, `DequeueAsync`, `HeartbeatAsync`, `ReclaimStaleJobsAsync` | |
| `JobProcessing/InMemoryJobRecoveryService.cs` | **DELETE** | Recovery merged into `IJobStore.ReclaimStaleJobsAsync` | |
| New | `InMemory/DependencyInjection/ServiceCollectionExtensions.cs` | Registration extension method `AddAsyncEndpointsInMemory()` | |
| `.csproj` | **FIX** | Change reference from `Core` → `Abstractions` | |

### 2.6 `AsyncEndpoints.Redis` → `AsyncEndpoints.Provider.Redis`

| Old File | Action | New File | Notes |
|----------|--------|----------|-------|
| `Configuration/RedisConfiguration.cs` | **KEEP** → `Provider.Redis/Configuration/` | | |
| `Extensions/ServiceCollectionExtensions.cs` | **REFACTOR** → `Provider.Redis/DependencyInjection/ServiceCollectionExtensions.cs` | Use new DI pattern | |
| `Services/IJobHashConverter.cs` | **REFACTOR** → `Provider.Redis/Internal/IJobHashConverter.cs` | Update for `JobRecord` | |
| `Services/IRedisLuaScriptService.cs` | **REWRITE** → `Provider.Redis/Internal/IRedisLuaScriptService.cs` | Update for new `IJobStore` contract | |
| `Services/JobHashConverter.cs` | **REWRITE** → `Provider.Redis/Internal/JobHashConverter.cs` | Update for `JobRecord` | |
| `Services/RedisJobRecoveryService.cs` | **DELETE** | Merged into `RedisJobStore.ReclaimStaleJobsAsync` | |
| `Services/RedisLuaScriptService.cs` | **REWRITE** → `Provider.Redis/Internal/RedisLuaScriptService.cs` | New Lua scripts for `EnqueueAsync`, `DequeueAsync` (with priority + channel), `HeartbeatAsync`, `ReclaimStaleJobsAsync` | **Major challenge** |
| `Storage/RedisJobStore.cs` | **REWRITE** → `Provider.Redis/Storage/RedisJobStore.cs` | Implement new `IJobStore` | |
| `.csproj` | **FIX** | Rename to `AsyncEndpoints.Provider.Redis`. Reference `Abstractions` (not aggregator) | |

### 2.7 `AsyncEndpoints` (Aggregator)

| Old File | Action | New File | Notes |
|----------|--------|----------|-------|
| `Extensions/ServiceCollectionExtensions.cs` | **DELETE/REWRITE** | Convert to a compatibility shim only. Forward calls to new packages. Mark `[Obsolete]` | |
| `.csproj` | **KEEP AS META PACKAGE** | Keep but don't add new logic. All references move to individual packages | Will be deprecated eventually |

### 2.8 Examples (Old → New)

| Old File | Action | Notes |
|----------|--------|-------|
| `examples/InMemoryExampleAPI/` | **UPDATE** | Change to new `IJobHandler<T>` pattern, new DI registration |
| `examples/RedisExampleAPI/` | **UPDATE** | Change to new `IJobHandler<T>` pattern, reference new package names |
| `examples/RedisExampleWorker/` | **UPDATE** | Same |

### 2.9 Tests (Old → New)

| Old File | Action | Notes |
|----------|--------|-------|
| `tests/AsyncEndpoints.UnitTests/` | **REWRITE in phases** | Tests must match new architecture. Each phase should keep relevant tests passing |
| `tests/AsyncEndpoints.Redis.UnitTests/` | **REWRITE** | Update for new `RedisJobStore` |
| `tests/AsyncEndpoints.Abstractions.UnitTests/` | **POPULATE** | Currently empty — add tests for new contracts |
| `tests/AsyncEndpoints.Core.UnitTests/` | **POPULATE** | Currently empty |
| `tests/AsyncEndpoints.AspNetCore.UnitTests/` | **POPULATE** | Currently empty |
| `tests/AsyncEndpoints.Worker.UnitTests/` | **POPULATE** | Currently empty |

---

## 3. Architecture Challenges (Categorized)

### Category A: Layer Violations (Must Fix)

#### A1: `Abstractions` references ASP.NET (`Microsoft.AspNetCore.Http.Abstractions`)
**Current**: `AsyncEndpoints.Abstractions.csproj` has `PackageReference Include="Microsoft.AspNetCore.Http.Abstractions"`.
**Problem**: Abstractions package must have zero dependencies. This makes it impossible for non-ASP.NET consumers (e.g., Console apps with Worker only) to use the abstractions.
**Impact**: Any project wanting to use `IJobStore` or `JobStatus` must bring in ASP.NET packages transitively.
**Fix**: Remove the ASP.NET dependency. The `Abstractions` project does not actually use any types from that package — the reference is likely leftover from an earlier iteration.

#### A2: `Core` references `Microsoft.AspNetCore.App`
**Current**: `AsyncEndpoints.Core.csproj` has `<FrameworkReference Include="Microsoft.AspNetCore.App" />`.
**Problem**: Core should be usable without ASP.NET. Worker-only deployments shouldn't need the ASP.NET runtime.
**Impact**: Worker services pull in ASP.NET assemblies unnecessarily.
**Fix**: Remove `FrameworkReference`. Move all code that uses `HttpContext`, `IResult`, `ProblemDetails`, etc. to `AspNetCore` project.

### Category B: Model/Schema Problems

#### B1: `Job` class has too many responsibilities
**Current**: `Job.cs` (in Core/JobProcessing) contains:
- HTTP metadata (`Headers`, `RouteParams`, `QueryParams`)
- State machine logic (`UpdateStatus`, `SetResult`, `SetError`, `IsValidStateTransition`)
- Copy semantics (`CreateCopy`)
- IDateTimeProvider dependency
- `AsyncEndpointError` dependency

**Problem**: Violates SRP. The `Job` class is both a data contract and a domain object. HTTP properties make it unusable without HTTP context.
**Fix**: Split into:
- `JobDescriptor` (in `Abstractions/Jobs/`) — lightweight submission model: `Name`, `Payload`, `Channel`, `Priority`, `PartitionKey`
- `JobRecord` (in `Abstractions/Jobs/`) — storage model: `JobId`, `Channel`, `Priority`, `Payload`, `Status`, timestamps, retry info, `WorkerId`, `LastHeartbeat`, `Result`, `ErrorMessage`
- State transition logic moves to `IJobStore` implementations or a dedicated validator

#### B2: Missing `DeadLettered` status
**Current**: `JobStatus` enum has `Queued`, `Scheduled`, `InProgress`, `Completed`, `Failed`, `Canceled`.
**Design Doc**: Lifecycle includes `DeadLettered` (max retries exceeded) as a distinct terminal state.
**Impact**: Can't distinguish "failed with retries remaining" from "exhausted all retries."
**Fix**: Add `DeadLettered` = `700` to `JobStatus` enum.

#### B3: Missing `Channel`, `Priority`, `PartitionKey` on job model
**Current**: `Job.cs` has no `Channel` or `Priority` or `Partition` fields.
**Design**: `JobRecord` must have `Channel` (string), `Priority` (int), and possibly `Partition` (int).
**Impact**: Channels and partitioning cannot be implemented without these fields on the stored job.
**Fix**: Add these fields to `JobRecord`. `JobDescriptor` carries them at submission time.

#### B4: Missing `LastHeartbeat` on job model
**Current**: No heartbeat field.
**Design**: `JobRecord` needs `LastHeartbeat` timestamp for worker liveness detection.
**Impact**: Stale job recovery cannot work reliably (current implementation uses `StartedAt` which is set once).
**Fix**: Add `LastHeartbeat` (DateTime?) to `JobRecord`.

### Category C: Interface Contract Problems

#### C1: `IJobStore` interface doesn't match design
**Current**:
```csharp
Task<MethodResult> CreateJob(Job job, CancellationToken ct);
Task<MethodResult<Job>> GetJobById(Guid id, CancellationToken ct);
Task<MethodResult> UpdateJob(Job job, CancellationToken ct);
Task<MethodResult<Job>> ClaimNextJobForWorker(Guid workerId, CancellationToken ct);
bool SupportsJobRecovery { get; }
Task<int> RecoverStuckJobs(long timeoutUnixTime, int maxRetries, CancellationToken ct);
```

**Design Required**:
```csharp
Task<JobId> EnqueueAsync(JobDescriptor descriptor, CancellationToken ct);
Task<JobRecord?> DequeueAsync(string channel, IReadOnlySet<int>? partitions, CancellationToken ct);
Task UpdateStatusAsync(JobId jobId, JobStatus status, string? result, CancellationToken ct);
Task<JobRecord?> GetStatusAsync(JobId jobId, CancellationToken ct);
Task HeartbeatAsync(JobId jobId, CancellationToken ct);
Task<int> ReclaimStaleJobsAsync(TimeSpan timeout, CancellationToken ct);
```

**Key Differences**:
- `CreateJob` → `EnqueueAsync` (takes `JobDescriptor`, returns `JobId`)
- `ClaimNextJobForWorker` → `DequeueAsync` (takes channel + partitions, no worker ID — design doesn't assign worker at dequeue)
- `UpdateJob` → `UpdateStatusAsync` (only status transitions, not full job replacement)
- `GetJobById` → `GetStatusAsync` (returns status + result, not full job)
- New: `HeartbeatAsync` (worker liveness)
- New: `ReclaimStaleJobsAsync` (sweeper reclaims jobs with expired heartbeats)
- Removed: `SupportsJobRecovery` (all stores must support reclaim via the interface; if not supported, return 0)

#### C2: `IJobManager` is unnecessary
**Current**: `IJobManager` wraps `IJobStore` with logging/metrics logic.
**Design**: No `IJobManager` equivalent. Orchestration handled by `JobSubmitter` (submission) and `JobWorkerService` (processing) talking directly to `IJobStore`.
**Impact**: Extra indirection layer removed. Logic in `JobManager` (retry calculation, success/failure handling) moves to appropriate new services.

#### C3: `IJobRecoveryService` is disconnected from `IJobStore`
**Current**: Separate interface with `RecoverStuckJobs` method. InMemory: `NotSupportedException`. Redis: separate class.
**Design**: `ReclaimStaleJobsAsync` is part of `IJobStore` itself. The sweeper service calls it, but the implementation is provider-specific.
**Impact**: Need to merge recovery into `IJobStore` and remove `IJobRecoveryService`.
**Fix**: Add `ReclaimStaleJobsAsync` to `IJobStore`. Remove `IJobRecoveryService` and its implementations.

#### C4: No `IJobListener` interface
**Current**: Worker directly calls `IJobManager.ClaimNextAvailableJob()`.
**Design**: Unified `IJobListener` with `WaitForNextJobAsync(channel, partitions, ct)`. Shipped with `PollingJobListener` implementation.
**Impact**: Need to create this interface and implementation.
**Fix**: Add `IJobListener` to `Abstractions/Listener/`. Add `PollingJobListener` to `Core/Listener/`.

#### C5: No `IJobSubmitter` interface
**Current**: Submission goes through `IJobManager.SubmitJob(jobName, payload, httpContext, ct)`.
**Design**: `IJobSubmitter.SubmitAsync<T>(T job, string? channel, object? partitionBy)` — clean, no HTTP dependency.
**Fix**: Add `IJobSubmitter` to `Abstractions/Submission/`. Implement `JobSubmitter` in `Core/Submission/`.

#### C6: No `IJobHandler<T>` interface
**Current**: `IAsyncEndpointRequestHandler<TRequest, TResponse>` takes `AsyncContext<TRequest>` which carries HTTP data.
**Design**: `IJobHandler<TJob>` with `Task HandleAsync(TJob job, CancellationToken ct)` — simple, no HTTP.
**Impact**: All consumer handler code must change.
**Fix**: Add `IJobHandler<T>` to `Abstractions/Jobs/`. Provide optional `AsyncContext<T>`-like wrapper in AspNetCore for consumers who need HTTP data.

### Category D: Worker Pipeline Problems

#### D1: Producer/Consumer Channel Pattern is Wrong
**Current**: The worker uses `System.Threading.Channels.Channel<Job>` as an in-process buffer:
```
JobProducerService ──► Channel<Job> ──► N × JobConsumerService
     (poll IJobStore)     (buffer)        (process handlers)
```
**Design**: Simple adaptive polling with backpressure:
```
JobWorkerService
    └── PollingJobListener.WaitForNextJobAsync(channel)
          └── IJobStore.DequeueAsync(channel)
          if (job) → process(job) → reset interval
          else → backoff → poll again
```
**Implications**:
- The in-process buffer is eliminated (backpressure done by not dequeuing when concurrency is saturated)
- Adaptive backoff requires tracking state per channel
- Multiple channels mean multiple polling loops

#### D2: Single `BackgroundService` for All Work vs. Per-Channel Workers
**Current**: One `AsyncEndpointsBackgroundService` with N consumers on a shared `Channel<Job>`.
**Design**: Each channel gets its own `BackgroundService` worker pool with its own `maxConcurrency`. Weighted channel consumption uses a single pool with round-robin.
**Challenge**: Need to support both models — dedicated per-channel workers AND weighted single-pool.

#### D3: Heartbeat and Sweeper are Missing
**Current**: `DistributedJobRecoveryService` runs periodically and recovers jobs based on `StartedAt` timestamp (set once at claim time). No per-job heartbeat.
**Design**:
- `HeartbeatService`: While a job is processing, background task periodically calls `IJobStore.HeartbeatAsync(jobId)` every ~30s.
- `StaleJobSweeper`: Runs every ~60s, calls `IJobStore.ReclaimStaleJobsAsync(timeout)`, which finds jobs in `Processing` state with `LastHeartbeat` older than threshold and resets them to `Queued`.
**Challenge**: Heartbeat service must be scoped per-active-job, not per-worker. Need to manage concurrent heartbeat loops.

### Category E: ASP.NET Coupling in Core

#### E1: `Serializer` depends on `Microsoft.AspNetCore.Http.Json.JsonOptions`
**Current**: `Serializer.cs` injects `IOptions<JsonOptions>` from `Microsoft.AspNetCore.Http.Json`.
**Fix**: Use `System.Text.Json.JsonSerializerOptions` directly. Configure via `AsyncEndpointsOptions`.

#### E2: Response types in Core (`JobResultResponse`, `ResponseDefaults`, `JobResponseMapper`)
**Current**: These implement `IResult` or reference `HttpContext`, but live in `Core/Utilities/`.
**Fix**: Move all to `AspNetCore/`.

#### E3: `JsonBodyParserService` depends on `HttpContext`
**Current**: In `Core/Infrastructure/Serialization/`. Parses body from `HttpContext.Request.Body`.
**Fix**: Move to `AspNetCore/`.

#### E4: `AsyncEndpointsResponseConfigurations` references `IResult` and `HttpContext`
**Current**: In `Core/Configuration/`. Contains factories for HTTP responses.
**Fix**: Move to `AspNetCore/Configuration/`. Or eliminate entirely — use standard ASP.NET patterns.

#### E5: `AsyncEndpointsJsonSerializationContext` references ASP.NET `ProblemDetails`
**Current**: Source-generator context includes `ProblemDetails` from `Microsoft.AspNetCore.Mvc`.
**Fix**: Remove `ProblemDetails` from the AOT context in Core. Move to AspNetCore if needed.

### Category F: Configuration Model Problems

#### F1: Fragmented Configuration
**Current**: 6 separate configuration classes:
- `AsyncEndpointsConfigurations` (aggregator)
- `AsyncEndpointsWorkerConfigurations`
- `AsyncEndpointsJobManagerConfigurations`
- `AsyncEndpointsResponseConfigurations`
- `AsyncEndpointsObservabilityConfigurations`
- `AsyncEndpointsRecoveryConfigurations`

**Design**: Single `AsyncEndpointsOptions` with inner builder pattern:
```csharp
class AsyncEndpointsOptionsBuilder {
    UsePostgres(conn)
    UseSqlServer(conn)
    UseRedis(conn)
    UseInMemory()
    Channels(Action<ChannelBuilder>)
    UsePartitioning(Action<PartitionOptions>)
    MaxConcurrency, MaxRetries, HeartbeatInterval, StaleJobTimeout, PollingMinInterval, PollingMaxInterval
}
```

**Fix**: Flatten configuration into `AsyncEndpointsOptions` in Core. `ChannelBuilder` and `PartitionOptions` in Core. `WorkerOptions` in Worker.

### Category G: DI Registration Problems

#### G1: Duplicate DI Registration
**Current**: Both `AsyncEndpoints.AspNetCore/Extensions/ServiceCollectionExtensions.cs` and `AsyncEndpoints/Extensions/ServiceCollectionExtensions.cs` define `AddAsyncEndpoints`, `AddAsyncEndpointsWorker`, `AddAsyncEndpointsInMemoryStore`.
**Problem**: Confusing, conflicting, and violates DRY.
**Fix**: Each project registers only its own services. Core registers core services. Worker registers worker services. AspNetCore registers endpoint services. Aggregator forwards calls.

#### G2: Wrong Layer for Provider Registration
**Current**: `AddAsyncEndpointsInMemoryStore()` is in `AsyncEndpoints.AspNetCore` (and Aggregator).
**Fix**: Provider registration should be in the provider package itself: `InMemory/DependencyInjection/ServiceCollectionExtensions.AddAsyncEndpointsInMemory()`.

#### G3: `AddAsyncEndpointHandler<>` is in AspNetCore (and Aggregator)
**Current**: Handler registration with `HandlerRegistrationTracker` (static global) happens in DI configuration.
**Fix**: Move registration to Core. `AddJobHandler<T>()` registers `IJobHandler<T>` in DI AND stores a typed delegate in `IHandlerRegistry`. The delegate captures `T` at compile time, handling typed deserialization + typed handler resolution — no reflection needed. This pattern is required for AOT compatibility.

#### G4: `HandlerRegistrationTracker` is a Static Global
**Current**: `ConcurrentDictionary`-based static registry for handler lookups.
**Problem**: Cannot be tested in isolation, leaks state between tests, not compatible with multi-tenant scenarios.
**Fix**: Replace static global with DI-registered `IHandlerRegistry` service. At registration time, `AddJobHandler<T>()` stores a delegate that captures `T` at compile time: the delegate deserializes the payload to `T` (using source-generated `JsonSerializerContext`), resolves `IJobHandler<T>` from DI, and invokes it. `JobDispatcher` looks up the delegate by job name and calls it — no runtime reflection. This is the same core pattern as the current `HandlerRegistrationTracker.Invoker<TRequest,TResponse>` but as a proper DI service.

### Category H: Channels and Partitions (New Features)

#### H1: No Channel Manager
**Required**: `ChannelManager` in Core — manages channel configurations, creates per-channel workers or weighted round-robin.
**Implementation**: `Core/Channels/ChannelManager.cs`

#### H2: No Partition Manager
**Required**: `PartitionManager` + `LeaseBasedPartitionAssigner` in Core for per-entity ordering.
**Implementation**: `Core/Partitioning/PartitionManager.cs`, `Core/Partitioning/LeaseBasedPartitionAssigner.cs`

#### H3: No Partition Lease Store
**Required**: Lease records stored in `IJobStore` or separate storage. `LeaseAcquire`, `LeaseRenew`, `LeaseRelease` operations.
**Impact**: `IJobStore` may need additional methods for partition leases, or partitioning is opt-in with its own `IPartitionLeaseStore`.

### Category I: Serialization Strategy

#### I1: `ISerializer` interface vs. direct `System.Text.Json`
**Current**: `ISerializer` wrapper around `System.Text.Json` with `IOptions<JsonOptions>`.
**Design Consideration**: Should serialization be pluggable (ISerializer) or fixed (STJ)?
**Recommendation**: Keep `ISerializer` in `Abstractions` for flexibility, but ship a STJ implementation in Core. Providers should accept `ISerializer` via DI.

#### I2: AOT Compatibility
**Current**: Multiple `JsonSerializerContext` source generators exist:
- `AsyncEndpointsJsonSerializationContext` in Core
- `ApplicationJsonSerializationContext` in each example project

**Challenges**:
1. New types (`JobRecord`, `JobDescriptor`, etc.) need to be added to the source-generated context.
2. `Serializer.cs` suppresses `IL2026`/`IL3050` on all non-generic overloads (`Deserialize(string, Type)`, `Serialize(object, Type)`). These use runtime `Type` with STJ = reflection, which breaks AOT.
3. `JobDispatcher` must not resolve `IJobHandler<T>` at runtime via reflection — it must use pre-registered typed delegates (see Category G).
4. Consumer DTOs (the `T` in `IJobHandler<T>`) cannot be known by the library's source generator — consumers need their own `JsonSerializationContext`.

**Fixes**:
1. Consolidate library types into `AsyncEndpoints.Serialization.JsonSerializationContext` in Core.
2. Remove non-generic overloads from `ISerializer` and `JobSerializer`. The typed delegates registered by `AddJobHandler<T>()` handle typed deserialization with `JsonSerializer.Deserialize<T>(payload, consumerContext.Default.T)`, eliminating the need for runtime-type serialization.
3. `JobDispatcher` uses `IHandlerRegistry` (compile-time delegates) instead of runtime DI resolution — no reflection.
4. Consumers must define a `[JsonSerializable(typeof(MyPayload))]` partial `JsonSerializerContext` and pass it during registration. Document this as a requirement in migration guide.

### Category J: Example/Consumer Impact

#### J1: Handler Interface Change
**Current handlers**:
```csharp
class MyHandler : IAsyncEndpointRequestHandler<MyRequest, MyResponse> {
    Task<MethodResult<MyResponse>> HandleAsync(AsyncContext<MyRequest> ctx, CancellationToken ct);
}
```
**New handlers**:
```csharp
class MyHandler : IJobHandler<MyRequest> {
    Task HandleAsync(MyRequest job, CancellationToken ct);
}
```
**Impact**: All consumer handler implementations must be rewritten.
**Migration**: Provide `IJobHandler<T>` as the primary contract. Support old `IAsyncEndpointRequestHandler` via an adapter for a transition period.

#### J2: `AsyncContext` Removal
**Current**: Handlers access HTTP headers, query params, route params via `AsyncContext`.
**New**: `IJobHandler<T>` receives only the deserialized `T`. HTTP metadata is not available.
**Migration**: 
- Option A: Consumers add needed HTTP data to their `T` payload DTO
- Option B: Add optional `JobRecord.Metadata` dictionary for non-payload data
- Option C: Provide `AspNetCore/Extensions/JobHandlerContext.cs` that wraps `IJobHandler<T>` and provides `HttpContext`-aware execution

**Recommendation**: Option B + C. Add `Dictionary<string, string>? Metadata` to `JobRecord` so headers can be forwarded if needed. Provide `AspNetCore` middleware that maps `HttpContext` onto `JobDescriptor.Metadata` before enqueue.

#### J3: Response Factory Pattern
**Current**: `AsyncEndpointsResponseConfigurations` allows custom response factories via `Func<Job, HttpContext, Task<IResult>>`.
**New**: Standard ASP.NET patterns — the endpoint returns `Results.Accepted()` or `Results.Ok()` directly.
**Impact**: Custom response configuration is lost. Consider if this is still needed or if standard ASP.NET response customization (via `IResult`) suffices.

### Category K: Observability

#### K1: Metrics/Tracing tied to old data model
**Current**: `AsyncEndpointsObservability` records metrics based on old `Job` class properties.
**Fix**: Update to use `JobRecord` and new statuses. The observability infrastructure is otherwise sound.

#### K2: No OpenTelemetry integration point
**Design**: Should provide standard OpenTelemetry metrics (job queue duration, processing duration, etc.) as a first-class feature.
**Fix**: Keep existing meter/activity infrastructure. Ensure `AsyncEndpointsOptions` has observability toggle.

### Category L: Provider Implementation Challenges

#### L1: InMemoryJobStore — Atomic Dequeue
**Current**: Uses `ConcurrentDictionary.TryUpdate` optimistic concurrency loop for claiming.
**New**: Need `DequeueAsync(channel, partitions)` that atomically dequeues the highest-priority, oldest job from a channel. With `ConcurrentDictionary`, this requires the same CAS loop pattern but filtered by channel+priority.

**Implementation considerations**:
- Jobs stored in a `ConcurrentDictionary<Guid, JobRecord>` for O(1) lookups
- Channel-based index: `ConcurrentDictionary<string, ConcurrentQueue<Guid>>` for FIFO within channel
- Or use `Channel<JobRecord>` per logical channel
- Partition support: filter by partition before dequeue

#### L2: RedisJobStore — Lua Scripts Rewrite
**Current**: Lua scripts implement `ClaimSingleJob` pattern matching old `IJobStore`.
**New**: Need scripts for:
- `EnqueueAsync`: HSET job hash + ZADD to channel's sorted set (score = priority*timestamp)
- `DequeueAsync`: ZPOPMIN from channel's sorted set + HGETALL job hash + HINCRBY status change
- `HeartbeatAsync`: HSET job heartbeat timestamp
- `ReclaimStaleJobsAsync`: ZRANGEBYSCORE in-progress set + update/retry logic
- Priority: score = (priority * 10^13) + unixTimestampMs per design doc

**Key Redis data structures**:
- `ae:job:{jobId}` — HASH with all job fields
- `ae:queue:{channel}` — SORTED SET scored by priority+timestamp
- `ae:inprogress` — SORTED SET scored by started-at timestamp (for sweeper)
- `ae:partition:{partitionId}:lease` — STRING with worker ID + expiry (for partitioning)

---

## 4. New Files to Create

### 4.1 `AsyncEndpoints.Abstractions` (New Files)

| File | Purpose |
|------|---------|
| `Jobs/IJobHandler.cs` | `interface IJobHandler<TJob> { Task HandleAsync(TJob job, CancellationToken ct); }` |
| `Jobs/JobDescriptor.cs` | Submission POCO: `Name`, `Payload` (string), `Channel` (string?), `Priority` (int), `PartitionKey` (object?) |
| `Jobs/JobRecord.cs` | Storage POCO: `JobId`, `Channel`, `Priority`, `Partition`, `Payload`, `Status`, `RetryCount`, `MaxRetries`, `CreatedAt`, `StartedAt`, `CompletedAt`, `WorkerId`, `LastHeartbeat`, `Result`, `ErrorMessage`, `Metadata` |
| `Jobs/JobStatus.cs` | Enum: Queued, Processing, Completed, Failed, DeadLettered (per design doc state machine). Note: `Scheduled` removed, `Processing` replaces `InProgress`, `Canceled` removed (use dead letter) |
| `Storage/IJobStore.cs` | New interface (see C1) |
| `Listener/IJobListener.cs` | `Task<JobRecord?> WaitForNextJobAsync(string channel, IReadOnlySet<int>? partitions, CancellationToken ct)` |
| `Submission/IJobSubmitter.cs` | `Task<JobId> SubmitAsync<T>(T job, string? channel = null, object? partitionBy = null, CancellationToken ct = default)` |
| `Partitioning/IPartitionAssigner.cs` | Interface for partition lease management |
| `Common/Result.cs` | Refined `Result<T>` type (simpler than current `MethodResult`) |

### 4.2 `AsyncEndpoints.Core` (New Files)

| File | Purpose |
|------|---------|
| `DependencyInjection/ServiceCollectionExtensions.cs` | `AddAsyncEndpointsCore()`, `AddJobStore<T>()`, etc. |
| `Configuration/AsyncEndpointsOptions.cs` | Flattened options |
| `Configuration/AsyncEndpointsOptionsBuilder.cs` | Builder pattern for progressive disclosure API |
| `Configuration/ChannelBuilder.cs` | Channel configuration builder |
| `Configuration/PartitionOptions.cs` | Partition configuration |
| `Submission/JobSubmitter.cs` | Implements `IJobSubmitter` |
| `Execution/IHandlerRegistry.cs` | Interface: `Register<T>(string, Func<...>)`, `GetInvoker(string)`. Stores compile-time typed delegates keyed by job name. |
| `Execution/HandlerRegistry.cs` | Implementation of `IHandlerRegistry`. Manages delegates that capture `T` at compile time (AOT-safe). |
| `Execution/JobDispatcher.cs` | Looks up typed delegate from `IHandlerRegistry` by job name, invokes it. Delegate handles typed deserialization + handler resolution (captures `T` at compile time via `AddJobHandler<T>()`). No runtime reflection. |
| `Listener/PollingJobListener.cs` | Adaptive polling implementation of `IJobListener` |
| `Partitioning/PartitionManager.cs` | Manages partition state, rebalancing |
| `Partitioning/LeaseBasedPartitionAssigner.cs` | Implements `IPartitionAssigner` for lease-based partition ownership |
| `Channels/ChannelManager.cs` | Manages channel configs, creates per-channel worker pools |
| `Serialization/JobSerializer.cs` | Serialization logic (wraps System.Text.Json) |
| `Internal/ServiceResolver.cs` | Internal service resolution helpers |

### 4.3 `AsyncEndpoints.Worker` (New Files)

| File | Purpose |
|------|---------|
| `Hosting/JobWorkerService.cs` | `BackgroundService` that polls `IJobListener` for one channel |
| `Hosting/WorkerOptions.cs` | Per-channel worker options |
| `Hosting/StaleJobSweeper.cs` | Periodic `BackgroundService` that calls `IJobStore.ReclaimStaleJobsAsync` |
| `Execution/JobExecutionPipeline.cs` | Orchestrates handler execution, error handling, status updates |
| `Execution/RetryHandler.cs` | Calculates retry delays, determines if retry is possible |
| `Heartbeat/HeartbeatService.cs` | Per-job heartbeat loop |
| `Concurrency/WorkerConcurrencyManager.cs` | Manages `SemaphoreSlim` per partition/channel |

### 4.4 `AsyncEndpoints.AspNetCore` (New Files)

| File | Purpose |
|------|---------|
| `Endpoints/JobEndpoints.cs` | Maps `POST /jobs` endpoint |
| `Endpoints/JobStatusEndpoint.cs` | Maps `GET /jobs/{id}` endpoint |
| `Endpoints/JobResultEndpoint.cs` | Maps `GET /jobs/{id}/result` endpoint (if applicable) |
| `Extensions/EndpointRouteBuilderExtensions.cs` | `MapAsyncEndpointsEndpoints("/jobs")` extension |
| `Extensions/HttpContextExtensions.cs` | Moved from Core |
| `Models/JobResponse.cs` | Response DTO for HTTP layer (moved from Core) |
| `Configuration/AspNetCoreOptions.cs` | AspNetCore-specific configuration |

---

## 5. Files to Delete (No Replacement)

| File | Reason |
|------|--------|
| `Core/JobProcessing/IJobManager.cs` | Replaced by `IJobSubmitter` + `IJobStore` |
| `Core/JobProcessing/JobManager.cs` | Replaced by `JobSubmitter` |
| `Core/JobProcessing/IJobRecoveryService.cs` | Merged into `IJobStore.ReclaimStaleJobsAsync` |
| `Core/JobProcessing/ErrorType.cs` | Not in design |
| `Core/Handlers/IAsyncEndpointRequestHandler.cs` | Replaced by `IJobHandler<T>` |
| `Core/Handlers/AsyncContext.cs` | Not in design (HTTP context not passed to handlers) |
| `Core/Handlers/HandlerRegistration.cs` | Registry pattern eliminated |
| `Core/Handlers/NoBodyRequest.cs` | Not needed with `IJobHandler<T>` |
| `Core/Background/IHandlerExecutionService.cs` | Replaced by `JobDispatcher` in Core/Execution |
| `Core/Background/IJobChannelEnqueuer.cs` | Channel pipeline removed |
| `Core/Background/IJobClaimingService.cs` | Claiming logic moved to `IJobStore.DequeueAsync` |
| `Core/Background/IJobConsumerService.cs` | Consumer pattern removed |
| `Core/Background/IJobProcessorService.cs` | Processing moved to Worker |
| `Core/Background/IJobProducerService.cs` | Producer pattern removed |
| `Core/Background/JobClaimingState.cs` | Enum no longer needed |
| `Core/Utilities/AsyncContextBuilder.cs` | Replaced by `JobDispatcher` |
| `Core/Utilities/HandlerRegistrationTracker.cs` | **RENAME + REFACTOR** → `Core/Execution/IHandlerRegistry.cs` + `HandlerRegistry.cs` | Replace static global with DI-registered service. Registry stores compile-time generated delegates keyed by job name — required for AOT-safe handler dispatch. |
| `Worker/Background/JobProducerService.cs` | Replaced by `PollingJobListener` |
| `Worker/Background/JobConsumerService.cs` | Replaced by `JobWorkerService` |
| `Worker/Background/JobClaimingService.cs` | Deleted |
| `Worker/Background/JobChannelEnqueuer.cs` | Deleted |
| `Worker/Background/JobProcessorService.cs` | Replaced by `JobExecutionPipeline` |
| `Worker/Background/HandlerExecutionService.cs` | Replaced by `JobExecutionPipeline` |
| `Worker/Background/DelayCalculatorService.cs` | Adaptive polling built into `PollingJobListener` |
| `Worker/Background/IDelayCalculatorService.cs` | Deleted |
| `Worker/Background/IJobProducerService.cs` | Deleted |
| `Worker/Background/IJobConsumerService.cs` | Deleted |
| `Worker/Background/IJobClaimingService.cs` | Deleted |
| `Worker/Background/IJobChannelEnqueuer.cs` | Deleted |
| `Worker/Background/IJobProcessorService.cs` | Deleted |
| `Worker/Background/IHandlerExecutionService.cs` | Deleted |
| `Worker/Background/DistributedJobRecoveryService.cs` | Replaced by `HeartbeatService` + `StaleJobSweeper` |
| `AspNetCore/Handlers/AsyncEndpointRequestDelegate.cs` | Replaced by simpler endpoint |
| `AspNetCore/Handlers/IAsyncEndpointRequestDelegate.cs` | Deleted |
| `Aggregator/Extensions/ServiceCollectionExtensions.cs` | (Keep as compatibility shim, mark `[Obsolete]`) |
| `Provider.InMemory/JobProcessing/InMemoryJobRecoveryService.cs` | Merged into store |
| `Provider.Redis/Services/RedisJobRecoveryService.cs` | Merged into `RedisJobStore.ReclaimStaleJobsAsync` |

---

## 6. Phased Execution Plan

### Phase 0: Analysis Complete ✅ (We are here)

### Phase 1: Abstractions Cleanup + Contract Foundation
**Goal**: Clean `Abstractions` project to zero dependencies, define all new interfaces, create new data models.

**Steps**:
1. Remove `Microsoft.AspNetCore.Http.Abstractions` from `Abstractions.csproj` — verify no types from that package are used
2. Create `Abstractions/Jobs/JobStatus.cs` — update to include `Processing` + `DeadLettered` states per design doc
3. Create `Abstractions/Jobs/JobDescriptor.cs` — submission input model
4. Create `Abstractions/Jobs/JobRecord.cs` — storage model (pure POCO, no methods)
5. Create `Abstractions/Jobs/IJobHandler.cs` — new generic handler interface
6. Create `Abstractions/Storage/IJobStore.cs` — new interface with `EnqueueAsync`, `DequeueAsync`, `UpdateStatusAsync`, `GetStatusAsync`, `HeartbeatAsync`, `ReclaimStaleJobsAsync`
7. Create `Abstractions/Listener/IJobListener.cs` — `WaitForNextJobAsync` interface
8. Create `Abstractions/Submission/IJobSubmitter.cs` — clean submission interface
9. Create `Abstractions/Partitioning/IPartitionAssigner.cs` — lease-based partition assignment
10. Review/Refactor `Abstractions/Common/Result.cs` — simplify, remove ASP.NET error patterns
11. Review/Keep `Abstractions/Common/JobError.cs` (was `AsyncEndpointError`) and exception types
12. Write unit tests for all new abstractions

**Validation**: `Abstractions` compiles with zero dependencies. All new interfaces compile.

**Test projects affected**: `AsyncEndpoints.Abstractions.UnitTests` (to be populated)

---

### Phase 2: Core ASP.NET Decoupling
**Goal**: Remove all ASP.NET dependencies from `Core`. Move HTTP-specific code to `AspNetCore`.

**Steps**:
1. Remove `FrameworkReference Include="Microsoft.AspNetCore.App"` from `Core.csproj`
2. **Move** `HttpContextExtensions.cs` → `AspNetCore/Extensions/HttpContextExtensions.cs`
3. **Move** `Infrastructure/Serialization/IJsonBodyParserService.cs` → `AspNetCore/Serialization/`
4. **Move** `Infrastructure/Serialization/JsonBodyParserService.cs` → `AspNetCore/Serialization/`
5. **Move** `Configuration/AsyncEndpointsResponseConfigurations.cs` → `AspNetCore/Configuration/`
6. **Move** `Utilities/JobResponse.cs` → `AspNetCore/Models/`
7. **Move** `Utilities/JobResponseMapper.cs` → `AspNetCore/Models/`
8. **Move** `Utilities/JobResultResponse.cs` → `AspNetCore/Endpoints/`
9. **Move** `Utilities/ResponseDefaults.cs` → `AspNetCore/Endpoints/`
10. **Refactor** `Serializer.cs` to remove `IOptions<JsonOptions>` from ASP.NET — use `System.Text.Json` directly
11. **Refactor** `AsyncEndpointsJsonSerializationContext.cs` — remove `ProblemDetails`, update for new types
12. **Update** `AspNetCore.csproj` — add `FrameworkReference` back (it needs ASP.NET)

**Validation**: `Core` compiles without `FrameworkReference`. `AspNetCore` compiles and includes all moved files.

**Test projects affected**: `AsyncEndpoints.Core.UnitTests`, `AsyncEndpoints.AspNetCore.UnitTests`

---

### Phase 3: Configuration Consolidation
**Goal**: Flatten 6 configuration classes into the `AsyncEndpointsOptions` builder pattern.

**Steps**:
1. Create `Core/Configuration/AsyncEndpointsOptions.cs` — holds all configuration values
2. Create `Core/Configuration/AsyncEndpointsOptionsBuilder.cs` — fluent builder
3. Create `Core/Configuration/ChannelBuilder.cs` — channel configuration
4. Create `Core/Configuration/PartitionOptions.cs` — partitioning configuration
5. Create `Worker/Hosting/WorkerOptions.cs` — worker-specific options
6. Delete old configuration classes:
   - `AsyncEndpointsConfigurations.cs`
   - `AsyncEndpointsWorkerConfigurations.cs`
   - `AsyncEndpointsJobManagerConfigurations.cs`
   - `AsyncEndpointsRecoveryConfigurations.cs`
   - `AsyncEndpointsObservabilityConfigurations.cs` (merge into options)
7. Update all existing references to old config classes to use new options
8. **Note**: `AsyncEndpointsResponseConfigurations.cs` already moved to AspNetCore in Phase 2 — refactor separately

**Validation**: Everything compiles with new options pattern.

**Test projects affected**: `AsyncEndpoints.Core.UnitTests`

---

### Phase 4: IJobStore Implementation Migration
**Goal**: Implement new `IJobStore` contract in all providers. Old implementations are preserved temporarily for backward compatibility.

**Steps**:
1. **InMemoryJobStore**: Rewrite to implement new `IJobStore`:
   - `EnqueueAsync`: Add `JobRecord` to dictionary + channel index
   - `DequeueAsync`: Atomic dequeue from channel sorted by priority → time
   - `UpdateStatusAsync`: Atomic status update
   - `GetStatusAsync`: Read status by ID
   - `HeartbeatAsync`: Update `LastHeartbeat`
   - `ReclaimStaleJobsAsync`: Scan for stale jobs, reset to Queued
2. **RedisJobStore**: Rewrite to implement new `IJobStore`:
   - New Lua scripts for `EnqueueAsync` (HSET + ZADD to channel set)
   - New Lua scripts for `DequeueAsync` (ZPOPMIN from channel set + HGETALL)
   - New Lua scripts for `HeartbeatAsync` (HSET)
   - New Lua scripts for `ReclaimStaleJobsAsync` (iterate in-progress set)
   - Remove old `ClaimSingleJob` Lua script
   - Remove `RedisJobRecoveryService`
3. Move `InMemory` project reference from `Core` → `Abstractions` in `.csproj`
4. Add reference from `Core` → `Abstractions.Storage` (newly defined)

**Validation**: All provider implementations compile against new `IJobStore`.

**Test projects affected**: `AsyncEndpoints.UnitTests` (InMemoryJobStore tests), `AsyncEndpoints.Redis.UnitTests`

---

### Phase 5: Core Orchestration Services
**Goal**: Build new orchestration services in Core.

**Steps**:
1. Create `Core/Submission/JobSubmitter.cs` (implements `IJobSubmitter`):
   - `SubmitAsync<T>`: Serializes job, creates `JobDescriptor`, calls `IJobStore.EnqueueAsync`
   - No `HttpContext` involvement
2. Create `Core/Listener/PollingJobListener.cs` (implements `IJobListener`):
   - Adaptive backoff: starts at `PollingMinInterval`, doubles on empty, resets on dequeue
   - Calls `IJobStore.DequeueAsync(channel, partitions)`
3. Create `Core/Execution/IHandlerRegistry.cs` + `HandlerRegistry.cs`:
   - DI-registered service (replaces static `HandlerRegistrationTracker`)
   - `Register<T>(string jobName, Func<IServiceProvider, JobRecord, CancellationToken, Task> handlerFactory)` — stores a typed delegate
   - `GetInvoker(string jobName) → Func<IServiceProvider, JobRecord, CancellationToken, Task>?` — lookup by job name
   - The delegate captures `T` at compile time and handles typed deserialization + typed handler resolution
4. Create `Core/Execution/JobDispatcher.cs`:
   - Injects `IHandlerRegistry`, `IServiceProvider`
   - `DispatchAsync(JobRecord record, CancellationToken ct)`:
     - Looks up invoker delegate from `IHandlerRegistry` by `record.JobName`
     - Invokes delegate with `IServiceProvider` and `JobRecord`
     - Returns success/failure for status update
   - No runtime reflection: all typed work (deserialization, handler resolution) happens inside the pre-registered delegate
5. Create `Core/Channels/ChannelManager.cs`:
   - Stores channel configurations (name, concurrency, retries)
   - Creates per-channel worker pools or weighted round-robin
6. Create `Core/Partitioning/PartitionManager.cs` + `LeaseBasedPartitionAssigner.cs`:
   - Manages partition leases
   - Handles rebalancing
7. Update `Core/DependencyInjection/ServiceCollectionExtensions.cs`:
   - Register all new services
   - Provide `AddAsyncEndpointsCore()` extension

**Validation**: Core compiles with new orchestration. Can construct `JobSubmitter` → `IJobStore` → `PollingJobListener` pipeline.

**Test projects affected**: `AsyncEndpoints.Core.UnitTests`

---

### Phase 6: Worker Engine Redesign
**Goal**: Replace Channel-based worker with direct polling.

**Steps**:
1. Create `Worker/Hosting/JobWorkerService.cs`:
   - `BackgroundService` that runs one polling loop
   - Uses `IJobListener.WaitForNextJobAsync(channel)` 
   - On job: acquire semaphore → dispatch → update status → release semaphore
   - When semaphore full: poller backs off (backpressure)
   - Strategy: support per-channel dedicated workers AND weighted round-robin
2. Create `Worker/Hosting/StaleJobSweeper.cs`:
   - Periodic `BackgroundService` calling `IJobStore.ReclaimStaleJobsAsync(staleTimeout)`
3. Create `Worker/Heartbeat/HeartbeatService.cs`:
   - Manages per-job heartbeat tasks
   - When handler starts processing: start heartbeat loop
   - When handler completes: stop heartbeat loop
   - Calls `IJobStore.HeartbeatAsync(jobId)` on interval
4. Create `Worker/Execution/JobExecutionPipeline.cs`:
   - Orchestrates: call `JobDispatcher` → on success: `UpdateStatusAsync(Completed, result)` → on failure: determine retry → `UpdateStatusAsync(Failed/Queued)` or `DeadLettered`
5. Create `Worker/Execution/RetryHandler.cs`:
   - Exponential backoff calculation
   - `ShouldRetry(JobRecord) → bool`
6. Create `Worker/Concurrency/WorkerConcurrencyManager.cs`:
   - Manages `SemaphoreSlim` per partition (for per-entity ordering)
   - Or per-channel semaphore (for concurrency limiting)
7. Delete old worker files (see Section 5)
8. Update `Worker/DependencyInjection/ServiceCollectionExtensions.cs`

**Validation**: Worker compiles. Can run polling loop. Backpressure works.

**Test projects affected**: `AsyncEndpoints.Worker.UnitTests` (to be populated), `AsyncEndpoints.UnitTests` (update existing)

---

### Phase 7: AspNetCore Endpoint Rewrite
**Goal**: Clean AspNetCore endpoints using new submission flow.

**Steps**:
1. Create `AspNetCore/Endpoints/JobEndpoints.cs`:
   - `POST /jobs`: Deserialize body, create `JobDescriptor`, submit via `IJobSubmitter`, return `202 Accepted`
   - Support channels via URL parameter or header
2. Create `AspNetCore/Endpoints/JobStatusEndpoint.cs`:
   - `GET /jobs/{id}`: Call `IJobStore.GetStatusAsync`, return status response
3. Create `AspNetCore/Endpoints/JobResultEndpoint.cs` (optional):
   - `GET /jobs/{id}/result`: Return result when completed
4. Create `AspNetCore/Extensions/EndpointRouteBuilderExtensions.cs`:
   - `MapAsyncEndpointsEndpoints("/jobs")` maps all three routes
5. Remove old handler-based routing (`MapAsyncPost<T>`, `MapAsyncPut`, etc.) or keep as transition shim
6. Update `AspNetCore/DependencyInjection/ServiceCollectionExtensions.cs`:
   - Register only AspNetCore-specific services
   - Remove worker/provider registrations (should be done by consumer)

**Validation**: AspNetCore endpoints work with new `IJobSubmitter`.

**Test projects affected**: `AsyncEndpoints.AspNetCore.UnitTests`

---

### Phase 8: Provider Renaming and Cleanup
**Goal**: Align provider naming and project references.

**Steps**:
1. **Rename** `src/AsyncEndpoints.Redis` → `src/AsyncEndpoints.Provider.Redis`:
   - Physical folder rename
   - Update `.csproj` to `AsyncEndpoints.Provider.Redis.csproj`
   - Update all namespaces: `AsyncEndpoints.Redis.*` → `AsyncEndpoints.Provider.Redis.*`
   - Update project reference to point to `Abstractions` (not Aggregator)
   - Update solution file
   - Update example projects' references
   - Update test references
2. **Refactor** `AsyncEndpoints.Provider.InMemory`:
   - Update `.csproj` to reference `Abstractions` instead of `Core`
   - Update namespaces to `AsyncEndpoints.Provider.InMemory.*`
   - Create `DependencyInjection/ServiceCollectionExtensions.cs` with `AddAsyncEndpointsInMemory()`
3. Ensure all providers follow `Storage/` and `DependencyInjection/` folder convention

**Validation**: All providers compile and are discoverable.

**Test projects affected**: `AsyncEndpoints.Redis.UnitTests` → `AsyncEndpoints.Provider.Redis.UnitTests`

---

### Phase 9: Aggregator Transition + Backward Compatibility
**Goal**: Keep `AsyncEndpoints` (meta package) as compatibility shim.

**Steps**:
1. Rewrite `src/AsyncEndpoints/Extensions/ServiceCollectionExtensions.cs`:
   - Mark all methods `[Obsolete("Use specific package registrations instead")]`
   - Forward calls to the new package DI methods
2. Keep `src/AsyncEndpoints/AsyncEndpoints.csproj` as meta package:
   - References: `Abstractions`, `Core`, `Worker`, `AspNetCore`, `Provider.InMemory`
   - Remove `Provider.Redis` reference (must be added explicitly)
3. Update NuGet metadata to indicate this is a legacy meta package

**Validation**: Existing consumers using `AsyncEndpoints` NuGet can still compile with warnings.

**Test projects affected**: None (existing tests may need update to remove `[Obsolete]` warnings)

---

### Phase 10: Examples, Docs, and Samples Update

**Steps**:
1. Update `examples/InMemoryExampleAPI/Program.cs`:
   - Use new `IJobHandler<T>` pattern
   - Use new DI registration methods
2. Update `examples/RedisExampleAPI/` + `examples/RedisExampleWorker/`:
   - Reference new provider package name
   - Use new handler pattern
3. Update `README.md` with new package names and registration examples
4. Create `samples/` directory structure if needed

---

### Phase 11: Test Restructuring

**Steps**:
1. Populate `AsyncEndpoints.Abstractions.UnitTests/` — test new contracts
2. Populate `AsyncEndpoints.Core.UnitTests/` — test orchestrations services
3. Populate `AsyncEndpoints.Worker.UnitTests/` — test worker pipeline
4. Populate `AsyncEndpoints.AspNetCore.UnitTests/` — test endpoint mapping
5. Update existing `AsyncEndpoints.UnitTests/` and `AsyncEndpoints.Redis.UnitTests/` for new implementations
6. Remove tests for deleted files (e.g., `JobManager` tests, `JobProducerService` tests). **Rewrite** (not delete) `HandlerRegistrationTracker` tests → `IHandlerRegistry` + `HandlerRegistry` tests.

---

## 7. Backward Compatibility Strategy

### 7.1 Handler Interface Migration (Critical)
**Challenge**: All consumers implement `IAsyncEndpointRequestHandler<TRequest, TResponse>`.
**Strategy**: 
- Keep old interfaces in `AspNetCore` namespace as deprecated
- Create an adapter: `AsyncEndpointRequestHandlerAdapter<TRequest, TResponse>` that implements `IJobHandler<TRequest>` by wrapping the old handler
- During transition, old handlers continue to work via the adapter
- Remove old interfaces in a future major version

### 7.2 Job Data Migration (Critical)
**Challenge**: Existing persisted jobs in Redis/other stores have the old schema (no Channel, Priority, LastHeartbeat, etc.).
**Strategy**: 
- New `JobRecord` schema is additive for in-memory.
- For Redis: Lua scripts should handle missing fields gracefully (use defaults: channel = "default", priority = 0, heartbeat = null).
- Old jobs will be processed with default values.

### 7.3 NuGet Package Transition
**Challenge**: Existing consumers reference `AsyncEndpoints` meta package.
**Strategy**: 
- Keep `AsyncEndpoints` meta package alive for at least 2 minor versions
- Each minor version adds `[Obsolete]` warnings pointing to new packages
- Major version bump removes the meta package

### 7.4 Configuration API Migration
**Challenge**: Existing `AddAsyncEndpoints(configureOptions)` pattern changes to `AddAsyncEndpointsCore().UseInMemory().WithWorker()`.
**Strategy**: 
- Provide overloads on old registrations that forward to new ones
- Document the breaking change prominently

---

## 8. Testing Strategy

### 8.1 Testing Principles
- **Contract tests**: Each `IJobStore` implementation must pass a common set of contract tests
- **Unit tests**: Test each service in isolation with mocked dependencies
- **Integration tests**: Test end-to-end with real (or embedded) stores

### 8.2 Contract Test Suite for `IJobStore`
Create a shared test suite that every provider must pass:
- `Enqueue_ShouldCreateJob_AndReturnJobId`
- `Dequeue_ShouldReturnNull_WhenQueueEmpty`
- `Dequeue_ShouldReturnJob_WhenJobAvailable`
- `Dequeue_ShouldRespectChannel_AndOnlyDequeueFromSpecifiedChannel`
- `Dequeue_ShouldReturnHighestPriorityJobFirst`
- `Dequeue_ShouldBeAtomic_TwoWorkersDontGetSameJob`
- `Heartbeat_ShouldUpdateLastHeartbeat`
- `ReclaimStaleJobs_ShouldReclaimJobs_WithExpiredHeartbeat`
- `GetStatus_ShouldReturnCurrentStatus`
- `UpdateStatus_ShouldTransitionState`

### 8.3 Phase-by-Phase Testing
| Phase | Tests to Add/Run |
|-------|-----------------|
| Phase 1 | Unit tests for all new Abstractions interfaces and models |
| Phase 2 | Verify Core compiles without ASP.NET; AspNetCore compiles with moved types |
| Phase 3 | Unit tests for new options and builder |
| Phase 4 | Contract tests for InMemoryJobStore and RedisJobStore against new `IJobStore` |
| Phase 5 | Unit tests for JobSubmitter, PollingJobListener, JobDispatcher |
| Phase 6 | Unit tests for JobWorkerService, HeartbeatService, StaleJobSweeper |
| Phase 7 | Integration tests for HTTP endpoints with in-memory store |
| Phase 8 | Provider contract tests with new names |
| Phase 9 | Verify `[Obsolete]` warnings appear correctly |

---

## 9. Risk Register

| # | Risk | Impact | Likelihood | Mitigation |
|---|------|--------|------------|------------|
| R1 | Breaking all consumer handlers (`IAsyncEndpointRequestHandler` → `IJobHandler<T>`) | High | Certain | Adapter pattern + 2-version deprecation |
| R2 | Backward compatibility of stored jobs (missing fields) | High | High | Default values in Redis Lua scripts, schema version field |
| R3 | `MethodResult<T>` vs new `Result<T>` confusion during transition | Medium | High | Keep both temporarily; `MethodResult` becomes thin wrapper |
| R4 | Performance regression from blocking fast consumers with Channel removal | Medium | Medium | Use SemaphoreSlim for backpressure; benchmark before/after |
| R5 | Channel/partitioning adds significant complexity | Medium | High | Phase it in: channels first (Phase 6), partitioning later (Phase 6b) |
| R6 | AOT compatibility broken by reflection-based handler resolution | High | Medium | Use `IHandlerRegistry` with compile-time typed delegates (not runtime DI resolution). Remove `ISerializer` non-generic overloads. Document consumer `JsonSerializerContext` requirement. |
| R7 | NuGet package fragmentation confuses consumers | Medium | Medium | Clear migration guide, meta package with `[Obsolete]` warnings |
| R8 | Redis Lua script changes break on Redis cluster | Medium | Low | Test against Redis Cluster in CI |
| R9 | Microsoft.AspNetCore.App removal from Core breaks existing consumers of `ISerializer` or other types | Low | Medium | Keep `Core` offering ASP.NET-friendly overloads at AspNetCore level |
| R10 | Test gap during transition (old tests deleted, new tests not yet written) | Medium | High | Phased deletion: keep old tests running until new ones cover same scenarios |

---

## 10. Definition of Done

- [ ] `AsyncEndpoints.Abstractions` has zero external dependencies
- [ ] `AsyncEndpoints.Core` has no `FrameworkReference` to `Microsoft.AspNetCore.App`
- [ ] All 6 new interfaces exist (`IJobStore`, `IJobListener`, `IJobSubmitter`, `IJobHandler<T>`, `IPartitionAssigner`, `ISerializer`) — only `ISerializer` existed before
- [ ] `JobDescriptor` + `JobRecord` replace old `Job` class completely
- [ ] Old `Job` class is deleted (no remaining references)
- [ ] `IJobManager` and `IJobRecoveryService` deleted
- [ ] All provider projects follow `AsyncEndpoints.Provider.*` naming
- [ ] All providers implement new `IJobStore` interface
- [ ] All worker files referencing Channel pattern are deleted
- [ ] `PollingJobListener` with adaptive backoff is implemented and tested
- [ ] `HeartbeatService` and `StaleJobSweeper` are implemented
- [ ] Channel support works (per-channel workers + weighted round-robin)
- [ ] Partition support works (hash-based + lease-based assignment)
- [ ] AspNetCore endpoints use `IJobSubmitter` (not `IJobManager`)
- [ ] `HandlerRegistrationTracker` (static global) is replaced by DI-registered `IHandlerRegistry` — typed delegates keyed by job name, preserving the AOT-safe compile-time delegate pattern
- [ ] `JobDispatcher` uses `IHandlerRegistry` lookup (no runtime reflection to resolve `IJobHandler<T>`)
- [ ] `ISerializer` non-generic overloads (`Deserialize(string, Type)`, `Serialize(object, Type)`) are removed — typed delegates handle their own serialization
- [ ] All handler registration is AOT-safe: `AddJobHandler<T>()` captures `T` at compile time in a typed delegate
- [ ] All examples are updated to use new API
- [ ] All tests pass (unit + integration)
- [ ] NuGet meta package provides backward compatibility with `[Obsolete]` warnings
- [ ] Migration guide written for consumers

---

## Appendix: Migration Guide for Consumers

### Before (Current API)
```csharp
// Program.cs
builder.Services
    .AddAsyncEndpoints()
    .AddAsyncEndpointsInMemoryStore()
    .AddAsyncEndpointHandler<MyHandler, MyRequest, MyResponse>("my-job")
    .AddAsyncEndpointsWorker();

app.MapAsyncPost<MyRequest>("my-job", "/api/my-job");
app.MapAsyncGetJobDetails();

// Handler
class MyHandler : IAsyncEndpointRequestHandler<MyRequest, MyResponse>
{
    public async Task<MethodResult<MyResponse>> HandleAsync(
        AsyncContext<MyRequest> context, CancellationToken ct)
    {
        var request = context.Request;
        var result = new MyResponse { ... };
        return MethodResult<MyResponse>.Success(result);
    }
}
```

### After (New API)
```csharp
// Program.cs
builder.Services
    .AddAsyncEndpointsCore()
    .UseInMemory()
    .AddAsyncEndpointsWorker()
    .AddJobHandler<MyHandler, MyRequest>("my-job");

var app = builder.Build();
app.MapAsyncEndpointsEndpoints("/jobs");

// Handler
class MyHandler : IJobHandler<MyRequest>
{
    public async Task HandleAsync(MyRequest job, CancellationToken ct)
    {
        // Process job directly — no AsyncContext wrapper
        await DoWorkAsync(job);
    }
}
```
