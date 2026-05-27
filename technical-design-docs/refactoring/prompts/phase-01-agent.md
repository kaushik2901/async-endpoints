# Agent Prompt: Phase 01 — Abstractions Cleanup + Contract Foundation

## Role
You are implementing Phase 01 of the AsyncEndpoints architecture realignment. You work with C#/.NET and are modifying the existing solution.

## Key Architectural Context
The solution has 7 projects (Abstractions, Core, AspNetCore, Worker, InMemory, Redis, Aggregator). The **target architecture** is a clean layered design:

```
AspNetCore ──► Core ──► Abstractions (zero deps)
Worker ──────► Core ──► Abstractions
Providers ────────────► Abstractions
```

The core data model replaces the monolithic `Job` class with two POCOs:
- `JobDescriptor` — submission input (JobName, Payload, Channel, Priority, PartitionKey)
- `JobRecord` — storage model (all fields: JobId, Channel, Priority, Status, timestamps, heartbeats, etc.)

Six new interfaces define the public contract:
- `IJobStore` (EnqueueAsync, DequeueAsync, UpdateStatusAsync, GetStatusAsync, HeartbeatAsync, ReclaimStaleJobsAsync)
- `IJobListener` (WaitForNextJobAsync)
- `IJobSubmitter` (SubmitAsync<T>)
- `IJobHandler<T>` (HandleAsync)
- `IPartitionAssigner` (AcquirePartitionAsync, RenewLeaseAsync, ReleasePartitionAsync)

`JobStatus` gains `Processing = 300` and `DeadLettered = 700` states.

**Critical constraint**: `Abstractions` package must have ZERO external dependencies. Currently it incorrectly references `Microsoft.AspNetCore.Http.Abstractions`.

## Current State Before Phase
- `Abstractions` references ASP.NET (violation)
- Old `Job` class in Core has HTTP properties, state machine logic
- Old `IJobManager`, `IJobRecoveryService` interfaces exist
- Old `MethodResult<T>` has HTTP-specific error fields
- New interfaces don't exist yet
- `JobStatus` missing `DeadLettered`

## Phase Goal
Clean `Abstractions` to zero deps. Define ALL new interfaces and data models. These become the foundation every other phase builds on.

## Task List (in order)

### 1.1 Remove ASP.NET dependency from Abstractions
- Edit `src/AsyncEndpoints.Abstractions/AsyncEndpoints.Abstractions.csproj`: remove `PackageReference Include="Microsoft.AspNetCore.Http.Abstractions"`
- Search all `.cs` files in Abstractions for `using Microsoft.AspNetCore.*` — remove any found
- `dotnet build` must succeed

### 1.2 Create `src/AsyncEndpoints.Abstractions/Jobs/JobStatus.cs`
```csharp
namespace AsyncEndpoints.Abstractions.Jobs;
public enum JobStatus { Queued = 100, Processing = 300, Completed = 500, Failed = 600, DeadLettered = 700 }
```

### 1.3 Create `src/AsyncEndpoints.Abstractions/Jobs/JobDescriptor.cs`
Simple POCO/record: `JobName`, `Payload` (string), `Channel` (string?, default "default"), `Priority` (int, default 0), `PartitionKey` (string?), `Metadata` (Dictionary<string,string>?)

### 1.4 Create `src/AsyncEndpoints.Abstractions/Jobs/JobRecord.cs`
Pure POCO with fields: `JobId` (Guid), `JobName`, `Channel`, `Priority`, `Partition` (int?), `Payload`, `Status`, `RetryCount`, `MaxRetries`, `CreatedAt`, `StartedAt`?, `CompletedAt`?, `WorkerId`?, `LastHeartbeat`?, `Result`?, `ErrorMessage`?, `Metadata`?

### 1.5 Create `src/AsyncEndpoints.Abstractions/Jobs/IJobHandler.cs`
```csharp
public interface IJobHandler<in TJob> { Task HandleAsync(TJob job, CancellationToken ct); }
```

### 1.6 Create `src/AsyncEndpoints.Abstractions/Storage/IJobStore.cs`
Methods: `EnqueueAsync(JobDescriptor, CancellationToken) → Guid`, `DequeueAsync(string channel, IReadOnlySet<int>? partitions, CancellationToken) → JobRecord?`, `UpdateStatusAsync(Guid, JobStatus, string? result, CancellationToken)`, `GetStatusAsync(Guid, CancellationToken) → JobRecord?`, `HeartbeatAsync(Guid, CancellationToken)`, `ReclaimStaleJobsAsync(TimeSpan staleTimeout, CancellationToken) → int`

### 1.7 Create `src/AsyncEndpoints.Abstractions/Listener/IJobListener.cs`
```csharp
Task<JobRecord?> WaitForNextJobAsync(string channel, IReadOnlySet<int>? partitions, CancellationToken ct);
```

### 1.8 Create `src/AsyncEndpoints.Abstractions/Submission/IJobSubmitter.cs`
```csharp
Task<Guid> SubmitAsync<T>(T job, string? channel = null, string? partitionKey = null, CancellationToken ct = default);
```

### 1.9 Create `src/AsyncEndpoints.Abstractions/Partitioning/IPartitionAssigner.cs`
Methods: `AcquirePartitionAsync(string workerId, string channel, CancellationToken) → int`, `RenewLeaseAsync(int partition, string workerId, CancellationToken)`, `ReleasePartitionAsync(int partition, string workerId, CancellationToken)`

### 1.10 Create `src/AsyncEndpoints.Abstractions/Common/Result.cs`
Simple discriminated union: `Result<T>.Success(T Value)` and `Result<T>.Failure(string Error)` — no HTTP-specific fields

### 1.11 Review existing `Common/AsyncEndpointError.cs`, `ExceptionInfo.cs`, `InnerExceptionInfo.cs` — keep as-is, verify no ASP.NET deps

### 1.12 Write unit tests in `tests/AsyncEndpoints.Abstractions.UnitTests/` for all new types

## Validation (must all pass)
- `dotnet build src/AsyncEndpoints.Abstractions/` succeeds
- `Abstractions.csproj` has zero package references
- No `using Microsoft.AspNetCore.*` anywhere in Abstractions/
- `dotnet test tests/AsyncEndpoints.Abstractions.UnitTests/` passes
- All 6 interfaces are public and compile

## Do NOT
- Modify Core, Worker, AspNetCore, or provider projects
- Delete old files yet (old code stays until later phases)
- Add any logic to the POCOs (JobDescriptor/JobRecord are data-only)
