# Phase 04: IJobStore Implementation Migration

**Goal**: Implement new `IJobStore` contract in all providers (InMemory + Redis). Old implementations are preserved temporarily for backward compatibility, but new implementations coexist.

**Prerequisites**: Phase 01 (needs `IJobStore` interface), Phase 03 (needs options pattern)

---

## Tasks

### 4.1 Implement InMemoryJobStore with new IJobStore

- [ ] Open `src/AsyncEndpoints.Provider.InMemory/JobProcessing/InMemoryJobStore.cs`
- [ ] Rewrite to implement the new `IJobStore` interface (from `Abstractions`)
- [ ] Data structures:
  - [ ] `ConcurrentDictionary<Guid, JobRecord>` for O(1) lookups
  - [ ] `ConcurrentDictionary<string, ConcurrentQueue<Guid>>` for channel-based FIFO indexing
  - [ ] Or use a `Channel<JobRecord>` per logical channel for simpler dequeue
- [ ] Implement methods:
  - [ ] `EnqueueAsync`: Create `JobRecord`, add to dictionary + channel index. Return `JobRecord.JobId`
  - [ ] `DequeueAsync`: Atomic dequeue from channel sorted by priority → time. Filter by partitions if provided
  - [ ] `UpdateStatusAsync`: Atomic status transition (validate valid state transitions)
  - [ ] `GetStatusAsync`: Read from dictionary by ID
  - [ ] `HeartbeatAsync`: Update `LastHeartbeat` on the `JobRecord`
  - [ ] `ReclaimStaleJobsAsync`: Scan for jobs in `Processing` state with `LastHeartbeat` older than threshold, reset to `Queued`

**Design notes**:
- State transitions: `Queued → Processing`, `Processing → Completed|Failed|Queued` (retry), `Failed → DeadLettered` (max retries exhausted)
- Use `ConcurrentDictionary.TryUpdate` CAS loop for atomicity — no locking

**Validation**: `InMemoryJobStore` implements `IJobStore`. Compiles.

---

### 4.2 Update InMemory.csproj to reference Abstractions

- [ ] Open `src/AsyncEndpoints.Provider.InMemory/AsyncEndpoints.Provider.InMemory.csproj`
- [ ] Change `ProjectReference` from `Core` → `Abstractions`
- [ ] Add `Core` reference **only if** it needs helpers from Core (e.g., `Serializer`) — prefer not to

**Validation**: InMemory project compiles with `Abstractions` as its only AsyncEndpoints dependency.

---

### 4.3 Remove InMemoryJobRecoveryService

- [ ] Delete `src/AsyncEndpoints.Provider.InMemory/JobProcessing/InMemoryJobRecoveryService.cs`
- [ ] Remove any references to it from DI registration

**Validation**: No references to `InMemoryJobRecoveryService` remain.

---

### 4.4 Create InMemory DI registration

- [ ] Create `src/AsyncEndpoints.Provider.InMemory/DependencyInjection/ServiceCollectionExtensions.cs`
- [ ] Define `AddAsyncEndpointsInMemory(this IServiceCollection services)` extension method
- [ ] Register `IJobStore` → `InMemoryJobStore` as singleton

**Validation**: `services.AddAsyncEndpointsInMemory()` registers `InMemoryJobStore` as `IJobStore`.

---

### 4.5 Rewrite RedisJobStore with new IJobStore

- [ ] Open `src/AsyncEndpoints.Provider.Redis/Storage/RedisJobStore.cs`
- [ ] Rewrite to implement new `IJobStore` interface
- [ ] Implement methods:
  - [ ] `EnqueueAsync`: HSET job hash + ZADD to channel sorted set (score = priority * 10^13 + timestamp)
  - [ ] `DequeueAsync`: ZPOPMIN from channel set + HGETALL job hash + status transition via Lua
  - [ ] `UpdateStatusAsync`: HSET status field
  - [ ] `GetStatusAsync`: HGETALL job hash
  - [ ] `HeartbeatAsync`: HSET heartbeat timestamp
  - [ ] `ReclaimStaleJobsAsync`: SCAN/iterate in-progress jobs, check heartbeat, reset stale ones via Lua

**Validation**: `RedisJobStore` implements `IJobStore`. Compiles.

---

### 4.6 Write new Lua scripts for Redis

- [ ] Create/update Lua scripts for:
  - [ ] `EnqueueAsync`: `redis.call('HSET', 'ae:job:'..jobId, ...); redis.call('ZADD', 'ae:queue:'..channel, score, jobId)`
  - [ ] `DequeueAsync`: Atomic pop + status update + return job hash
  - [ ] `HeartbeatAsync`: `redis.call('HSET', 'ae:job:'..jobId, 'LastHeartbeat', timestamp)`
  - [ ] `ReclaimStaleJobsAsync`: Find stale processing jobs, reset to Queued status
- [ ] Remove old `ClaimSingleJob` Lua script

**Validation**: Scripts load into Redis successfully. `SCRIPT LOAD` returns SHA hashes.

---

### 4.7 Remove RedisJobRecoveryService

- [ ] Delete `src/AsyncEndpoints.Provider.Redis/Services/RedisJobRecoveryService.cs`
- [ ] Remove references from DI registration

**Validation**: No references to `RedisJobRecoveryService` remain.

---

### 4.8 Update Redis DI registration

- [ ] Update `src/AsyncEndpoints.Provider.Redis/DependencyInjection/ServiceCollectionExtensions.cs`
- [ ] Register `IJobStore` → `RedisJobStore`
- [ ] Register `RedisLuaScriptService` as needed

**Validation**: `services.AddAsyncEndpointsRedis()` registers `RedisJobStore` as `IJobStore`.

---

### 4.9 Move Redis project reference (update .csproj)

- [ ] Open Redis `.csproj`
- [ ] Change reference from `AsyncEndpoints` (aggregator) → `Abstractions`
- [ ] Add reference to `Core` only if needed (serialization helpers)

**Validation**: Redis project compiles with `Abstractions` as main dependency.

---

### 4.10 Write contract tests for IJobStore

- [ ] Create shared contract test suite in `tests/AsyncEndpoints.UnitTests/ContractTests/`:
  - [ ] `Enqueue_ShouldCreateJob_AndReturnJobId`
  - [ ] `Dequeue_ShouldReturnNull_WhenQueueEmpty`
  - [ ] `Dequeue_ShouldReturnJob_WhenJobAvailable`
  - [ ] `Dequeue_ShouldRespectChannel_FiltersByChannel`
  - [ ] `Dequeue_ShouldReturnHighestPriorityJobFirst`
  - [ ] `Dequeue_ShouldBeAtomic_TwoWorkersDontGetSameJob`
  - [ ] `Heartbeat_ShouldUpdateLastHeartbeat`
  - [ ] `ReclaimStaleJobs_ShouldReclaimJobs_WithExpiredHeartbeat`
  - [ ] `GetStatus_ShouldReturnCurrentStatus`
  - [ ] `UpdateStatus_ShouldTransitionState_Valid`
  - [ ] `UpdateStatus_ShouldReject_InvalidTransition`
- [ ] Run contract tests against both InMemory and Redis implementations

**Validation**: Both providers pass the same contract test suite.

---

### 4.11 Keep old IJobStore implementations for backward compat (optional)

- [ ] If needed, keep old implementations under a different class name (e.g., `LegacyInMemoryJobStore`)
- [ ] Or keep old files until all references are updated (per transition strategy)

**Validation**: Old consumers that reference old types still compile during transition.

---

## Phase 04 Definition of Done

- [ ] `InMemoryJobStore` fully implements new `IJobStore`
- [ ] `RedisJobStore` fully implements new `IJobStore`
- [ ] Both providers pass the shared contract test suite
- [ ] `InMemoryJobRecoveryService` deleted
- [ ] `RedisJobRecoveryService` deleted
- [ ] Old Lua scripts replaced with new ones
- [ ] InMemory.csproj references `Abstractions` (not `Core`)
- [ ] Redis.csproj references `Abstractions` (not aggregator)
- [ ] DI registration methods exist in provider packages
- [ ] `dotnet build` succeeds for both providers
- [ ] Contract tests pass for both providers

**Next phase**: [Phase 05: Core Orchestration Services](phase-05-core-orchestration.md)
