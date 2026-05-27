# Agent Prompt: Phase 06 — Worker Engine Redesign

## Role
You are implementing Phase 06 of the AsyncEndpoints architecture realignment. You replace the Channel-based producer/consumer worker pipeline with a direct polling model.

## Key Architectural Context
**Old pipeline** (being deleted):
```
JobProducerService ──► Channel<Job> ──► N × JobConsumerService
     (poll store)          (buffer)        (process handlers + heartbeats + recovery)
```

**New pipeline**:
```
JobWorkerService (BackgroundService per channel)
    └── PollingJobListener.WaitForNextJobAsync(channel)
          └── IJobStore.DequeueAsync(channel)
    if job: acquire semaphore → heartbeat → dispatch → update status → release
    else: adaptive backoff
    
StaleJobSweeper (separate BackgroundService)
    └── IJobStore.ReclaimStaleJobsAsync(timeout)  // periodic
```

Key changes:
- No in-process `Channel<T>` buffer — backpressure via semaphore
- Each channel gets its own `BackgroundService` worker pool with `maxConcurrency`
- Heartbeat per active job (not per worker)
- Retry logic in `RetryHandler` with exponential backoff
- `StaleJobSweeper` is a separate background service

## Current State Before Phase
- Abstractions, Core clean (Phases 01-03)
- IJobStore implementations work (Phase 04)
- Core orchestration services exist (Phase 05)
- Old worker files still present: `Background/` contains 10+ files with Channel pattern

## Phase Goal
Build new Worker services. Delete all old `Background/` files. Wire via DI. Unit tests pass.

## Task List

### 6.1 Create `Worker/Hosting/JobWorkerService.cs`
- `BackgroundService` with polling loop: call `IJobListener.WaitForNextJobAsync`
- On job: acquire semaphore → start heartbeat → `JobDispatcher.DispatchAsync` → on success: `UpdateStatusAsync(Completed)` / on failure: retry or dead-letter → stop heartbeat → release semaphore

### 6.2 Create `Worker/Concurrency/WorkerConcurrencyManager.cs`
- `SemaphoreSlim` per channel (and per partition if partitioning enabled)
- Methods: `WaitAsync(channel, partition?, ct)`, `Release(channel, partition?)`, `CurrentCount(channel)`

### 6.3 Create `Worker/Heartbeat/HeartbeatService.cs`
- `StartHeartbeat(Guid jobId, ct) → IAsyncDisposable`
- Background loop calls `IJobStore.HeartbeatAsync(jobId)` every interval until disposed

### 6.4 Create `Worker/Execution/RetryHandler.cs`
- `ShouldRetry(JobRecord) → bool`: `RetryCount < MaxRetries`
- `GetRetryDelay(JobRecord) → TimeSpan`: exponential backoff

### 6.5 Create `Worker/Execution/JobExecutionPipeline.cs`
- Orchestrate: `JobDispatcher.DispatchAsync` → success: Completed / retryable: re-queue / exhausted: DeadLettered

### 6.6 Create `Worker/Hosting/StaleJobSweeper.cs`
- `BackgroundService`: periodic call to `IJobStore.ReclaimStaleJobsAsync(timeout)`

### 6.7 Create `Worker/DependencyInjection/ServiceCollectionExtensions.cs`
- `AddAsyncEndpointsWorker(this IServiceCollection, Action<WorkerOptions>? configure = null)`
- Register: `JobWorkerService` (IHostedService), `StaleJobSweeper` (IHostedService), `HeartbeatService`, `RetryHandler`, `JobExecutionPipeline`, `WorkerConcurrencyManager`, `WorkerOptions`

### 6.8 Delete ALL old files in `Worker/Background/` (see task file for full list: 10+ files with Channel pattern, interfaces, DelayCalculator, DistributedJobRecoveryService)

### 6.9 Write unit tests for all new Worker services

## Validation
- `dotnet build src/AsyncEndpoints.Worker/` succeeds
- Worker starts, polls, processes jobs, shuts down gracefully
- Heartbeats fire per active job
- Stale sweeper reclaims jobs with expired heartbeats
- No old `Background/` files exist
- `dotnet test tests/AsyncEndpoints.Worker.UnitTests/` passes

## Do NOT
- Modify AspNetCore endpoints (Phase 07)
- Rename provider folders (Phase 08)
- Modify Core orchestration (already done in Phase 05)
