# Phase 11: Test Restructuring

**Goal**: Ensure all test projects are populated, old tests are updated or removed, and the full test suite passes.

**Prerequisites**: All preceding phases (tests must match the final architecture)

---

## Tasks

### 11.1 Populate AsyncEndpoints.Abstractions.UnitTests

- [ ] Create test classes if not already done in Phase 01:
  - [ ] `JobStatusTests` — enum values, parsing
  - [ ] `JobDescriptorTests` — construction, defaults
  - [ ] `JobRecordTests` — construction, serialization round-trip
  - [ ] `ResultTTests` — success/failure pattern, implicit conversion
  - [ ] `InterfaceSignatureTests` — reflection-based verification that all interfaces match spec

**Validation**: Tests cover all types in `Abstractions`. `dotnet test` passes.

---

### 11.2 Populate AsyncEndpoints.Core.UnitTests

- [ ] Add/update tests (should have been started in Phases 03 and 05):
  - [ ] **Configuration tests** (Phase 03):
    - [ ] `AsyncEndpointsOptions` defaults
    - [ ] `AsyncEndpointsOptionsBuilder` fluent API
    - [ ] `ChannelBuilder`
    - [ ] `PartitionOptions`
  - [ ] **JobSubmitter tests** (Phase 05):
    - [ ] `SubmitAsync_SerializesAndEnqueues`
    - [ ] `SubmitAsync_UsesDefaultChannel`
    - [ ] `SubmitAsync_UsesSpecifiedPartitionKey`
  - [ ] **PollingJobListener tests** (Phase 05):
    - [ ] `WaitForNextJobAsync_CallsDequeue`
    - [ ] `AdaptiveBackoff_ResetsOnSuccess`
    - [ ] `AdaptiveBackoff_DoublesOnEmpty`
    - [ ] `AdaptiveBackoff_RespectsMaxInterval`
    - [ ] `Cancellation_StopsWaiting`
  - [ ] **JobDispatcher tests** (Phase 05):
    - [ ] `DispatchAsync_ResolvesHandler`
    - [ ] `DispatchAsync_DeserializesPayload`
    - [ ] `DispatchAsync_HandlerNotFound_FailsGracefully`
    - [ ] `DispatchAsync_HandlerThrows_ReturnsFailure`
  - [ ] **ChannelManager tests** (Phase 05):
    - [ ] `GetChannelNames_ReturnsConfigured`
    - [ ] `GetChannelConfig_ByName`
  - [ ] **PartitionManager tests** (Phase 05):
    - [ ] `AssignWorkerToPartition`
    - [ ] `ReleasePartition`
  - [ ] **Serializer tests**:
    - [ ] `Serialize_Deserialize_RoundTrip`
    - [ ] `CustomOptions_AreApplied`

**Validation**: All Core services have unit test coverage. `dotnet test` passes.

---

### 11.3 Populate AsyncEndpoints.Worker.UnitTests

- [ ] Add tests (should have been started in Phase 06):
  - [ ] **JobWorkerService tests**:
    - [ ] `ExecuteAsync_PollsAndProcesses`
    - [ ] `ExecuteAsync_Backpressure`
    - [ ] `ExecuteAsync_GracefulShutdown`
  - [ ] **HeartbeatService tests**:
    - [ ] `StartHeartbeat_PeriodicUpdates`
    - [ ] `Dispose_StopsHeartbeatLoop`
  - [ ] **RetryHandler tests**:
    - [ ] `ShouldRetry_BoundaryConditions`
    - [ ] `GetRetryDelay_ExponentialBackoff`
  - [ ] **JobExecutionPipeline tests**:
    - [ ] `Success_UpdatesStatusToCompleted`
    - [ ] `RetryableFailure_Requeues`
    - [ ] `NonRetryableFailure_DeadLetters`
    - [ ] `StoreException_DoesNotUpdateStatus`
  - [ ] **StaleJobSweeper tests**:
    - [ ] `ExecuteAsync_CallsReclaimStaleJobs`
    - [ ] `LogsReclaimedCount`
  - [ ] **WorkerConcurrencyManager tests**:
    - [ ] `WaitAsync_BlocksAtMax`
    - [ ] `Release_AllowsNext`
    - [ ] `PerPartitionConcurrency`

**Validation**: All Worker services have unit test coverage. `dotnet test` passes.

---

### 11.4 Populate AsyncEndpoints.AspNetCore.UnitTests

- [ ] Add tests (should have been started in Phase 07):
  - [ ] **Endpoint integration tests** (using `WebApplicationFactory`):
    - [ ] `POST /jobs — 202 Accepted`
    - [ ] `POST /jobs — 400 BadRequest`
    - [ ] `GET /jobs/{id} — 200 OK`
    - [ ] `GET /jobs/{id} — 404 NotFound`
    - [ ] `GET /jobs/{id}/result — Returns result`
  - [ ] **Endpoint route builder tests**:
    - [ ] `MapAsyncEndpointsEndpoints_MapsAllRoutes`
  - [ ] **Moved type tests** (from Phase 02):
    - [ ] `HttpContextExtensions`
    - [ ] `JsonBodyParserService`
    - [ ] `JobResponseMapper`

**Validation**: AspNetCore endpoint behavior is verified. `dotnet test` passes.

---

### 11.5 Update existing unit tests (AsyncEndpoints.UnitTests)

- [ ] Open existing `tests/AsyncEndpoints.UnitTests/`
- [ ] Identify tests that exercise deleted/changed components:
  - [ ] `JobManager` tests → delete or rewrite for `JobSubmitter`
  - [ ] `Job` class tests → rewrite as `JobRecord` + `JobDescriptor` tests
  - [ ] `IJobStore` tests → update to new interface contract
  - [ ] `HandlerRegistrationTracker` tests → delete
  - [ ] Old worker pipeline tests → delete
- [ ] Keep tests for components that survived:
  - [ ] Observability tests (updated for new data model)
  - [ ] Serialization tests (updated for new types)
- [ ] Add contract tests for `IJobStore` (from Phase 04) if not yet present

**Validation**: Existing `AsyncEndpoints.UnitTests` has relevant, up-to-date tests. Deleted files' test coverage is removed.

---

### 11.6 Update Redis tests

- [ ] Open `tests/AsyncEndpoints.Redis.UnitTests/` (or renamed to `AsyncEndpoints.Provider.Redis.UnitTests/`)
- [ ] Update test references to new `RedisJobStore` implementation
- [ ] Update contract tests to match new `IJobStore` interface
- [ ] Add tests for:
  - [ ] `EnqueueAsync` with Lua script
  - [ ] `DequeueAsync` with channel and priority
  - [ ] `HeartbeatAsync`
  - [ ] `ReclaimStaleJobsAsync`
- [ ] Remove tests for deleted components:
  - [ ] `RedisJobRecoveryService` tests
  - [ ] Old claim-single-job tests

**Validation**: `dotnet test` on Redis tests passes.

---

### 11.7 Remove tests for deleted files

- [ ] Identify and delete test files for:
  - [ ] `IJobManager` / `JobManager`
  - [ ] `IJobRecoveryService` / `DistributedJobRecoveryService`
  - [ ] `IAsyncEndpointRequestHandler`
  - [ ] `HandlerRegistration` / `HandlerRegistrationTracker`
  - [ ] `AsyncContext` / `AsyncContextBuilder`
  - [ ] `NoBodyRequest`
  - [ ] `ErrorType`
  - [ ] `JobClaimingState`
  - [ ] All old `Background/` interfaces and implementations
  - [ ] Old `IJobStore` implementation tests (if they don't match new interface)

**Validation**: No tests reference deleted types. `dotnet build` succeeds for all test projects.

---

### 11.8 Run full test suite

- [ ] `dotnet test` on entire solution
- [ ] Investigate and fix any test failures
- [ ] Ensure no warnings related to `[Obsolete]` APIs being tested (update tests or suppress appropriately)

**Validation**: `dotnet test` passes with zero failures across all test projects.

---

## Phase 11 Definition of Done

- [ ] `AsyncEndpoints.Abstractions.UnitTests` — populated
- [ ] `AsyncEndpoints.Core.UnitTests` — populated
- [ ] `AsyncEndpoints.Worker.UnitTests` — populated
- [ ] `AsyncEndpoints.AspNetCore.UnitTests` — populated
- [ ] `AsyncEndpoints.UnitTests` — updated (old tests removed, new contract tests added)
- [ ] `AsyncEndpoints.Redis.UnitTests` (or renamed) — updated
- [ ] No test files reference deleted types
- [ ] Full `dotnet test` suite passes with zero failures

**Final phase**: 🎉 Architecture realignment complete!

---

## Post-Completion Verification

After Phase 11, verify against the overall Definition of Done:

- [ ] `AsyncEndpoints.Abstractions` has zero external dependencies
- [ ] `AsyncEndpoints.Core` has no `FrameworkReference` to `Microsoft.AspNetCore.App`
- [ ] All 6 new interfaces exist (`IJobStore`, `IJobListener`, `IJobSubmitter`, `IJobHandler<T>`, `IPartitionAssigner`, `ISerializer`)
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
- [ ] `HandlerRegistrationTracker` (static global) is eliminated
- [ ] All examples are updated to use new API
- [ ] All tests pass (unit + integration)
- [ ] NuGet meta package provides backward compatibility with `[Obsolete]` warnings
- [ ] Migration guide written for consumers
