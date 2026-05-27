# Agent Prompt: Phase 04 — IJobStore Implementation Migration

## Role
You are implementing Phase 04 of the AsyncEndpoints architecture realignment. You rewrite both InMemory and Redis job stores to implement the new `IJobStore` interface from Abstractions.

## Key Architectural Context
The new `IJobStore` contract (defined in Phase 01):
```
EnqueueAsync(JobDescriptor, ct) → Guid
DequeueAsync(channel, partitions, ct) → JobRecord?
UpdateStatusAsync(jobId, status, result?, ct)
GetStatusAsync(jobId, ct) → JobRecord?
HeartbeatAsync(jobId, ct)
ReclaimStaleJobsAsync(staleTimeout, ct) → int  // count reclaimed
```

State machine: `Queued → Processing → Completed | Failed → Queued (retry) → DeadLettered`

Dequeue must be **atomic** — only one worker gets a given job. For InMemory, use `ConcurrentDictionary.TryUpdate` CAS loop. For Redis, use Lua scripts.

## Current State Before Phase
- `IJobStore` interface exists in Abstractions (Phase 01)
- Old implementations exist: `InMemoryJobStore` (old `IJobStore` from Core) and `RedisJobStore` (old interface)
- Old `InMemoryJobRecoveryService` and `RedisJobRecoveryService` exist (separate from store)
- InMemory.csproj references Core (should reference Abstractions)
- Redis.csproj references Aggregator (should reference Abstractions)

## Phase Goal
Both providers implement new `IJobStore` correctly. Old recovery services deleted. Project references fixed. Contract tests pass for both.

## Task List

### 4.1 Rewrite `InMemory/Storage/InMemoryJobStore.cs`
- Implement `IJobStore` from `AsyncEndpoints.Abstractions.Storage`
- Data: `ConcurrentDictionary<Guid, JobRecord>` + `ConcurrentDictionary<string, ConcurrentQueue<Guid>>` for channel indexing
- `EnqueueAsync`: create JobRecord from descriptor, add to dict + channel queue
- `DequeueAsync`: CAS loop — try to dequeue highest-priority oldest job from channel queue, set status to Processing
- `UpdateStatusAsync`: validate state transition, update via CAS
- `GetStatusAsync`: direct dict lookup
- `HeartbeatAsync`: update `LastHeartbeat`
- `ReclaimStaleJobsAsync`: scan Processing jobs, if LastHeartbeat older than threshold → reset to Queued

### 4.2 Fix `InMemory.csproj`: change ProjectReference from Core → Abstractions

### 4.3 Delete `InMemory/JobProcessing/InMemoryJobRecoveryService.cs`

### 4.4 Create `InMemory/DependencyInjection/ServiceCollectionExtensions.cs`
- `AddAsyncEndpointsInMemory(this IServiceCollection)` — register `IJobStore → InMemoryJobStore` as singleton

### 4.5 Rewrite `Provider.Redis/Storage/RedisJobStore.cs`
- Implement new `IJobStore`
- Redis keys: `ae:job:{jobId}` (HASH), `ae:queue:{channel}` (SORTED SET scored by priority*10^13+timestamp)
- `EnqueueAsync`: HSET + ZADD via Lua
- `DequeueAsync`: ZPOPMIN + HGETALL + status update via Lua
- `HeartbeatAsync`: HSET via Lua
- `ReclaimStaleJobsAsync`: ZRANGEBYSCORE in-progress set + reset stale ones

### 4.6 Write Lua scripts for each operation (or update existing)
Remove old `ClaimSingleJob` Lua script.

### 4.7 Delete `Redis/Services/RedisJobRecoveryService.cs`

### 4.8 Update Redis DI registration — register `IJobStore → RedisJobStore`

### 4.9 Fix `Redis.csproj`: reference Abstractions (not Aggregator)

### 4.10 Write/run contract tests in `AsyncEndpoints.UnitTests/ContractTests/`
Tests: `Enqueue_ShouldCreateJob_AndReturnJobId`, `Dequeue_ShouldReturnNull_WhenEmpty`, `Dequeue_ShouldRespectChannel`, `Dequeue_ShouldReturnHighestPriorityFirst`, `Dequeue_ShouldBeAtomic`, `Heartbeat_ShouldUpdateLastHeartbeat`, `ReclaimStaleJobs_ShouldReclaimExpired`, `GetStatus_ShouldReturnCurrentStatus`, `UpdateStatus_ShouldTransitionState`

## Validation
- Both stores compile against new `IJobStore`
- Both pass the same contract test suite
- `InMemoryJobRecoveryService` deleted (no references)
- `RedisJobRecoveryService` deleted (no references)
- `dotnet build` succeeds
- `dotnet test` passes for all provider tests

## Do NOT
- Modify Core orchestration (JobSubmitter, PollingJobListener — that's Phase 05)
- Delete old `Background/` worker files (Phase 06)
- Rename provider folders yet (Phase 08)
