# Agent Prompt: Phase 11 — Test Restructuring

## Role
You are implementing the final Phase 11 of the AsyncEndpoints architecture realignment. You ensure test coverage across all projects, clean up tests for deleted components, and verify the full suite passes.

## Key Architectural Context
By this phase, all new code is in place across all projects:
- Abstractions: interfaces + data models
- Core: orchestration services (JobSubmitter, PollingJobListener, JobDispatcher, ChannelManager, PartitionManager)
- Worker: polling engine (JobWorkerService, HeartbeatService, StaleJobSweeper, RetryHandler, JobExecutionPipeline, WorkerConcurrencyManager)
- AspNetCore: Minimal API endpoints
- Providers: InMemory + Redis implement new IJobStore

Old components **deleted**: JobManager, IJobRecoveryService, all Background/ Channel-pattern files, IAsyncEndpointRequestHandler, AsyncContext, etc.

Old components **refactored** (not deleted):
- `HandlerRegistrationTracker` (static global) → `IHandlerRegistry` + `HandlerRegistry` (DI-registered service in Core). The delegate-registry pattern is preserved because it's required for AOT-safe handler dispatch.

Tests for deleted components must be removed. Tests for new components must exist and pass.

## Current State Before Phase
- Some unit tests may have been written incrementally (Phases 01-10)
- Old tests for deleted components may still exist
- `AsyncEndpoints.Abstractions.UnitTests/` — may be empty or partial
- `AsyncEndpoints.Core.UnitTests/` — may be empty or partial (needs `IHandlerRegistry` + `HandlerRegistry` tests)
- `AsyncEndpoints.Worker.UnitTests/` — may be empty
- `AsyncEndpoints.AspNetCore.UnitTests/` — may have integration tests from Phase 07
- `AsyncEndpoints.UnitTests/` — has old tests that may reference deleted types
- `AsyncEndpoints.Redis.UnitTests/` — has old tests for old IJobStore

## Phase Goal
All test projects populated and passing. No tests reference deleted types. Full `dotnet test` passes.

## Task List

### 11.1 Populate `AsyncEndpoints.Abstractions.UnitTests/`
- `JobStatusTests` — enum values match expected integers
- `JobDescriptorTests` — construction, defaults, null handling
- `JobRecordTests` — construction, serialization round-trip
- `ResultTTests` — success/failure pattern matching
- `InterfaceSignatureTests` — reflection-based: verify all 6 interfaces have correct method signatures

### 11.2 Populate `AsyncEndpoints.Core.UnitTests/`
- Options/builder tests (from Phase 03)
- `JobSubmitterTests` — SubmitAsync serializes, calls EnqueueAsync, returns Guid, uses default channel
- `PollingJobListenerTests` — calls DequeueAsync, adaptive backoff reset/double/max, cancellation
- `JobDispatcherTests` — resolves handler, deserializes payload, handler-not-found, handler-throws
- `ChannelManagerTests` — config storage, lookup
- `PartitionManagerTests` — acquire/release partition (mock IPartitionAssigner)

### 11.3 Populate `AsyncEndpoints.Worker.UnitTests/`
- `JobWorkerServiceTests` — polling loop, processes job, backpressure when concurrency full
- `HeartbeatServiceTests` — periodic heartbeat, dispose stops loop
- `RetryHandlerTests` — ShouldRetry boundary, exponential backoff calculation
- `JobExecutionPipelineTests` — success→Completed, retry→Queued, exhausted→DeadLettered, store exception→no update
- `StaleJobSweeperTests` — calls ReclaimStaleJobsAsync on interval
- `WorkerConcurrencyManagerTests` — semaphore blocks at max, release allows next, per-partition scoping

### 11.4 Populate `AsyncEndpoints.AspNetCore.UnitTests/`
- Integration tests using WebApplicationFactory:
  - POST /jobs → 202 Accepted with jobId
  - POST /jobs → 400 BadRequest for invalid
  - GET /jobs/{id} → 200 OK with status
  - GET /jobs/{id} → 404 NotFound

### 11.5 Update `AsyncEndpoints.UnitTests/`
- Remove tests for: `JobManager`, `Job` (old class), old `IJobStore`, old worker pipeline
- **Rewrite** (not delete) `HandlerRegistrationTracker` tests → `IHandlerRegistry` + `HandlerRegistry` tests
- Keep/update tests for: Observability (update for new data model), Serialization (update for new types)
- Add contract tests for `IJobStore` (run against InMemory provider)

### 11.6 Update `AsyncEndpoints.Redis.UnitTests/`
- Update to test new `RedisJobStore` implementation
- Add tests for Lua-script-based Enqueue, Dequeue, Heartbeat, ReclaimStaleJobs
- Remove tests for: `RedisJobRecoveryService`, old claim-single-job

### 11.7 Remove tests for deleted types / rewrite tests for refactored types
- Search for test files/classes referencing deleted types: `IJobManager`, `JobManager`, `IJobRecoveryService`, `IAsyncEndpointRequestHandler`, `HandlerRegistration`, `AsyncContext`, `NoBodyRequest`, `ErrorType`, `JobClaimingState`, `AsyncContextBuilder`, `JobProducerService`, `JobConsumerService`, `JobClaimingService`, `JobChannelEnqueuer`, `JobProcessorService`, `HandlerExecutionService`, `DelayCalculatorService`, `DistributedJobRecoveryService`, `InMemoryJobRecoveryService`, `RedisJobRecoveryService`
- Delete each found test
- Search for test files/classes referencing **refactored** `HandlerRegistrationTracker` — **rewrite** as `IHandlerRegistry` + `HandlerRegistry` tests (do not delete; the delegate-registry pattern is preserved for AOT safety)

### 11.8 Run full test suite and fix failures

## Validation
- `dotnet test` passes with **zero failures** across ALL test projects
- No test files reference any deleted type
- Contract tests pass for both InMemory and Redis providers
- All new services have unit test coverage

## Do NOT
- Change implementation code (only tests)
- Add new features
- Remove any remaining production code that should stay
