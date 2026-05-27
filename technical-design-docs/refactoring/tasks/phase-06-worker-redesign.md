# Phase 06: Worker Engine Redesign

**Goal**: Replace Channel-based producer/consumer worker with direct polling via `PollingJobListener`.

**Prerequisites**: Phase 01 (interfaces), Phase 04 (IJobStore), Phase 05 (PollingJobListener, JobDispatchter)

---

## Tasks

### 6.1 Create JobWorkerService

- [ ] Create `src/AsyncEndpoints.Worker/Hosting/JobWorkerService.cs`
- [ ] Inherit from `BackgroundService`
- [ ] Constructor injects: `IJobListener`, `JobDispatcher`, `IJobStore`, `IOptions<WorkerOptions>`, `ILogger<JobWorkerService>`, `WorkerConcurrencyManager`
- [ ] `ExecuteAsync(CancellationToken stoppingToken)`:
  - [ ] Polling loop: call `IJobListener.WaitForNextJobAsync(channel, partitions, ct)`
  - [ ] If job returned:
    - [ ] Acquire concurrency semaphore (via `WorkerConcurrencyManager`)
    - [ ] Start heartbeat loop (via `HeartbeatService`)
    - [ ] Dispatch job via `JobDispatcher`
    - [ ] On success → `IJobStore.UpdateStatusAsync(Completed, result, ct)`
    - [ ] On failure → determine retry via `RetryHandler`, update status accordingly
    - [ ] Stop heartbeat loop
    - [ ] Release concurrency semaphore
  - [ ] If no job: backoff (handled by `PollingJobListener`'s adaptive backoff)
- [ ] Support strategies:
  - [ ] **Per-channel dedicated workers**: One `JobWorkerService` per channel, each with its own semaphore
  - [ ] **Weighted round-robin**: Single `JobWorkerService` that round-robins across channels

**Validation**: `JobWorkerService` starts, polls, processes jobs, and shuts down gracefully.

---

### 6.2 Create WorkerConcurrencyManager

- [ ] Create `src/AsyncEndpoints.Worker/Concurrency/WorkerConcurrencyManager.cs`
- [ ] Manages `SemaphoreSlim` instances:
  - [ ] Per-channel semaphore for concurrency limiting
  - [ ] Per-partition semaphore for per-entity ordering (if partitioning enabled)
- [ ] Methods:
  - [ ] `WaitAsync(string channel, int? partition, CancellationToken ct)`
  - [ ] `Release(string channel, int? partition)`
  - [ ] `CurrentCount(string channel) → int`

**Validation**: Concurrency manager correctly limits concurrent jobs per channel.

---

### 6.3 Create HeartbeatService

- [ ] Create `src/AsyncEndpoints.Worker/Heartbeat/HeartbeatService.cs`
- [ ] Constructor injects: `IJobStore`
- [ ] `StartHeartbeat(Guid jobId, CancellationToken ct)`:
  - [ ] Creates a background loop that calls `IJobStore.HeartbeatAsync(jobId)` every `HeartbeatInterval`
  - [ ] Returns an `IAsyncDisposable` that stops the loop on dispose
- [ ] Handle graceful stop — ensure final heartbeat is sent

**Validation**:
```csharp
await using var heartbeat = await heartbeatService.StartHeartbeat(jobId, ct);
// After HeartbeatInterval, LastHeartbeat is updated
```

---

### 6.4 Create RetryHandler

- [ ] Create `src/AsyncEndpoints.Worker/Execution/RetryHandler.cs`
- [ ] Methods:
  - [ ] `ShouldRetry(JobRecord record) → bool` — returns true if `record.RetryCount < record.MaxRetries`
  - [ ] `GetRetryDelay(JobRecord record) → TimeSpan` — exponential backoff: `baseDelay * 2^retryCount`
- [ ] Configurable via `WorkerOptions.MaxRetries` and base delay

**Validation**:
```csharp
Assert.True(retryHandler.ShouldRetry(new JobRecord { RetryCount = 0, MaxRetries = 3 }));
Assert.False(retryHandler.ShouldRetry(new JobRecord { RetryCount = 3, MaxRetries = 3 }));
```

---

### 6.5 Create JobExecutionPipeline

- [ ] Create `src/AsyncEndpoints.Worker/Execution/JobExecutionPipeline.cs`
- [ ] Orchestrates the full lifecycle of a single job execution:
  - [ ] Call `JobDispatcher.DispatchAsync(record, ct)`
  - [ ] On success:
    - [ ] Call `IJobStore.UpdateStatusAsync(jobId, JobStatus.Completed, result, ct)`
  - [ ] On handler exception:
    - [ ] Increment retry count
    - [ ] If `RetryHandler.ShouldRetry(record)`:
      - [ ] Call `IJobStore.UpdateStatusAsync(jobId, JobStatus.Queued, errorMessage, ct)` — re-queue for retry
    - [ ] Else:
      - [ ] Call `IJobStore.UpdateStatusAsync(jobId, JobStatus.DeadLettered, errorMessage, ct)`
  - [ ] On unexpected exception (store failure):
    - [ ] Log critical error, do NOT update status (job may be retried by sweeper)

**Validation**: Pipeline correctly handles success, retryable failure, and dead-letter scenarios.

---

### 6.6 Create StaleJobSweeper

- [ ] Create `src/AsyncEndpoints.Worker/Hosting/StaleJobSweeper.cs`
- [ ] Inherit from `BackgroundService`
- [ ] Constructor injects: `IJobStore`, `IOptions<WorkerOptions>`
- [ ] `ExecuteAsync(CancellationToken stoppingToken)`:
  - [ ] Loop: sleep `StaleJobTimeout / 2` (or configurable interval)
  - [ ] Call `IJobStore.ReclaimStaleJobsAsync(StaleJobTimeout, ct)`
  - [ ] Log number of reclaimed jobs
- [ ] Should be a separate `BackgroundService` from `JobWorkerService`

**Validation**: Sweeper starts, calls reclaim periodically, shuts down gracefully.

---

### 6.7 Create Worker DI registration

- [ ] Create/Update `src/AsyncEndpoints.Worker/DependencyInjection/ServiceCollectionExtensions.cs`
- [ ] Define `AddAsyncEndpointsWorker(this IServiceCollection services, Action<WorkerOptions>? configure = null)`
- [ ] Register:
  - [ ] `JobWorkerService` as `IHostedService`
  - [ ] `StaleJobSweeper` as `IHostedService`
  - [ ] `HeartbeatService` (singleton)
  - [ ] `RetryHandler` (singleton)
  - [ ] `JobExecutionPipeline` (singleton)
  - [ ] `WorkerConcurrencyManager` (singleton)
  - [ ] `WorkerOptions` via options pattern
- [ ] Support factory for per-channel dedicated workers: `AddChannelWorker(string channelName, int maxConcurrency)`

**Validation**: `services.AddAsyncEndpointsWorker()` registers all worker services. `JobWorkerService` starts as hosted service.

---

### 6.8 Delete old worker files

- [ ] Delete `src/AsyncEndpoints.Worker/Background/AsyncEndpointsBackgroundService.cs`
- [ ] Delete `src/AsyncEndpoints.Worker/Background/JobProducerService.cs`
- [ ] Delete `src/AsyncEndpoints.Worker/Background/JobConsumerService.cs`
- [ ] Delete `src/AsyncEndpoints.Worker/Background/JobClaimingService.cs`
- [ ] Delete `src/AsyncEndpoints.Worker/Background/JobChannelEnqueuer.cs`
- [ ] Delete `src/AsyncEndpoints.Worker/Background/JobProcessorService.cs`
- [ ] Delete `src/AsyncEndpoints.Worker/Background/HandlerExecutionService.cs`
- [ ] Delete `src/AsyncEndpoints.Worker/Background/DelayCalculatorService.cs`
- [ ] Delete all corresponding interface files:
  - [ ] `IJobProducerService.cs`, `IJobConsumerService.cs`, `IJobClaimingService.cs`
  - [ ] `IJobChannelEnqueuer.cs`, `IJobProcessorService.cs`, `IHandlerExecutionService.cs`
  - [ ] `IDelayCalculatorService.cs`
- [ ] Delete `src/AsyncEndpoints.Worker/Background/DistributedJobRecoveryService.cs`
- [ ] Remove old `Background/` directory if empty

**Validation**: All old worker files deleted. `dotnet build` succeeds on Worker project.

---

### 6.9 Write unit tests

- [ ] In `tests/AsyncEndpoints.Worker.UnitTests/` (populate from scratch):
  - [ ] `JobWorkerServiceTests`:
    - [ ] `ExecuteAsync_LoopsUntilCancelled`
    - [ ] `ExecuteAsync_ProcessesJob_WhenDequeued`
    - [ ] `ExecuteAsync_Backpressure_WhenConcurrencyFull`
  - [ ] `HeartbeatServiceTests`:
    - [ ] `StartHeartbeat_SendsHeartbeats_OnInterval`
    - [ ] `StartHeartbeat_Dispose_StopsHeartbeat`
  - [ ] `RetryHandlerTests`:
    - [ ] `ShouldRetry_ReturnsTrue_WhenRetryCountLessThanMax`
    - [ ] `ShouldRetry_ReturnsFalse_WhenRetryCountExceeded`
    - [ ] `GetRetryDelay_ExponentialBackoff`
  - [ ] `JobExecutionPipelineTests`:
    - [ ] `ExecuteAsync_Success_UpdatesStatusToCompleted`
    - [ ] `ExecuteAsync_RetryableFailure_RequeuesJob`
    - [ ] `ExecuteAsync_NonRetryableFailure_DeadLetters`
  - [ ] `StaleJobSweeperTests`:
    - [ ] `ExecuteAsync_CallsReclaimStaleJobs`
  - [ ] `WorkerConcurrencyManagerTests`:
    - [ ] `WaitAsync_Blocks_WhenAtMaxConcurrency`
    - [ ] `Release_AllowsNextJob`

**Validation**: `dotnet test tests/AsyncEndpoints.Worker.UnitTests/` passes.

---

## Phase 06 Definition of Done

- [ ] `JobWorkerService` polls `IJobListener` and processes jobs
- [ ] `HeartbeatService` sends periodic heartbeats for active jobs
- [ ] `StaleJobSweeper` periodically reclaims stale jobs
- [ ] `RetryHandler` determines retry viability with exponential backoff
- [ ] `JobExecutionPipeline` orchestrates the full lifecycle (dispatch → success/failure → retry/dead-letter)
- [ ] `WorkerConcurrencyManager` limits concurrent processing
- [ ] All old `Background/` files are deleted
- [ ] `AddAsyncEndpointsWorker()` registers all services
- [ ] `dotnet build` succeeds on Worker project
- [ ] Unit tests pass for all new services

**Next phase**: [Phase 07: AspNetCore Endpoint Rewrite](phase-07-aspnetcore-endpoints.md)
