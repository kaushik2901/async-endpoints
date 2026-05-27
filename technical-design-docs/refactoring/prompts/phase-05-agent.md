# Agent Prompt: Phase 05 — Core Orchestration Services

## Role
You are implementing Phase 05 of the AsyncEndpoints architecture realignment. You build the new orchestration services in Core: `JobSubmitter`, `PollingJobListener`, `JobDispatcher`, `ChannelManager`, and partitioning support.

## Key Architectural Context
The new Core layer implements all orchestration logic:
- **JobSubmitter**: Serializes job payload → creates `JobDescriptor` → calls `IJobStore.EnqueueAsync`
- **PollingJobListener**: Adaptive polling loop over `IJobStore.DequeueAsync` with exponential backoff
- **IHandlerRegistry**: Stores compile-time typed delegates keyed by job name (AOT-safe — replaces static `HandlerRegistrationTracker`)
- **JobDispatcher**: Looks up typed delegate from `IHandlerRegistry` by job name, invokes it. The delegate (captured at `AddJobHandler<T>()` compile time) handles typed deserialization + `IJobHandler<T>` resolution.
- **ChannelManager**: Manages named channel configurations
- **PartitionManager** + **LeaseBasedPartitionAssigner**: Partition lease management

## Current State Before Phase
- Abstractions has all interfaces (Phase 01)
- Core is clean of ASP.NET deps (Phase 02)
- Options model exists (Phase 03)
- IJobStore implementations exist in providers (Phase 04)
- No orchestration services exist yet — old `JobManager` still in Core (to be deleted later)

## Phase Goal
Build all Core orchestration services. Wire them via DI in `ServiceCollectionExtensions`. Write unit tests.

## Task List

### 5.1 Create `Core/Submission/JobSubmitter.cs`
- Implements `IJobSubmitter`
- Injects: `IJobStore`, `ISerializer`
- `SubmitAsync<T>`: serialize T → JobDescriptor → `IJobStore.EnqueueAsync` → return Guid

### 5.2 Create `Core/Listener/PollingJobListener.cs`
- Implements `IJobListener`
- Injects: `IJobStore`, `IOptions<AsyncEndpointsOptions>`
- Adaptive backoff: success → reset to min; empty → double up to max
- `WaitForNextJobAsync`: call `IJobStore.DequeueAsync`, apply backoff, return `JobRecord?`

### 5.3 Create `Core/Execution/IHandlerRegistry.cs` + `HandlerRegistry.cs`
- Injects: (none needed — just a `ConcurrentDictionary`)
- `Register<T>(string jobName, Func<IServiceProvider, JobRecord, CancellationToken, Task> invoker)` — stores a typed delegate
- `GetInvoker(string jobName) → Func<IServiceProvider, JobRecord, CancellationToken, Task>?` — lookup by name
- The delegate captures `T` at compile time (set up by `AddJobHandler<T>()`) and handles typed deserialization + typed `IJobHandler<T>` resolution internally. This avoids runtime reflection.

### 5.4 Create `Core/Execution/JobDispatcher.cs`
- Injects: `IServiceProvider`, `IHandlerRegistry`
- `DispatchAsync(JobRecord record)`: look up invoker from `IHandlerRegistry` by `record.JobName` → invoke → return success/failure
- Handle missing handler gracefully (mark job failed)
- **No runtime reflection**: the typed work (deserialization, `IJobHandler<T>` resolution) happens inside the pre-registered delegate

### 5.5 Create `Core/Channels/ChannelManager.cs`
- Stores channel configs (name, maxConcurrency, maxRetries, partitions)
- Methods: `GetChannelNames()`, `GetChannelConfig(name)`, `GetConfiguredChannels()`

### 5.6 Create `Core/Partitioning/PartitionManager.cs` + `LeaseBasedPartitionAssigner.cs`
- `PartitionManager`: assign/release/get partitions for workers
- `LeaseBasedPartitionAssigner`: implements `IPartitionAssigner`, lease acquire/renew/release

### 5.7 Create/update `Core/DependencyInjection/ServiceCollectionExtensions.cs`
- `AddAsyncEndpointsCore(this IServiceCollection, Action<AsyncEndpointsOptionsBuilder>? configure = null)`
- Register: `IJobSubmitter → JobSubmitter`, `IJobListener → PollingJobListener`, `IHandlerRegistry → HandlerRegistry`, `JobDispatcher`, `ChannelManager`, `ISerializer → JobSerializer`, `AsyncEndpointsOptions`

### 5.8 Write unit tests for all services

## Validation
- `dotnet build src/AsyncEndpoints.Core/` succeeds
- All services can be resolved from DI via `AddAsyncEndpointsCore()`
- `dotnet test tests/AsyncEndpoints.Core.UnitTests/` passes

## Do NOT
- Modify Worker project (Phase 06)
- Modify AspNetCore endpoints (Phase 07)
- Delete old `JobManager.cs` yet (will be removed in a cleanup pass)
