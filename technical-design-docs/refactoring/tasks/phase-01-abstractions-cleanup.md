# Phase 01: Abstractions Cleanup + Contract Foundation

**Goal**: Clean `Abstractions` project to zero dependencies, define all new interfaces, create new data models.

**Prerequisites**: None

---

## Tasks

### 1.1 Remove ASP.NET dependency from Abstractions

- [ ] Open `src/AsyncEndpoints.Abstractions/AsyncEndpoints.Abstractions.csproj`
- [ ] Remove `PackageReference Include="Microsoft.AspNetCore.Http.Abstractions"`
- [ ] Search all `.cs` files in the project for any `using` statements referencing ASP.NET types (`Microsoft.AspNetCore.*`)
- [ ] Remove or fix any references found
- [ ] Run `dotnet build` on the project

**Validation**: `Abstractions.csproj` has zero package references. `dotnet build` succeeds. No `using Microsoft.AspNetCore.*` statements remain.

---

### 1.2 Create JobStatus enum (updated)

- [ ] Create `src/AsyncEndpoints.Abstractions/Jobs/JobStatus.cs`
- [ ] Define enum with values: `Queued = 100`, `Processing = 300`, `Completed = 500`, `Failed = 600`, `DeadLettered = 700`
- [ ] Remove old `JobStatus` from `Core/JobProcessing/JobStatus.cs` (or keep as deprecated alias)

**Validation**:
```csharp
// Verification snippet
var status = JobStatus.Queued;
Debug.Assert(Enum.IsDefined(typeof(JobStatus), status));
```

---

### 1.3 Create JobDescriptor class

- [ ] Create `src/AsyncEndpoints.Abstractions/Jobs/JobDescriptor.cs`
- [ ] Properties: `JobName` (string), `Payload` (string), `Channel` (string?, default `"default"`), `Priority` (int, default `0`), `PartitionKey` (string?), `Metadata` (Dictionary<string, string>?)
- [ ] Make it a simple `record` or POCO with no methods

**Validation**: `JobDescriptor` compiles. Can instantiate: `new JobDescriptor { JobName = "test", Payload = "{}" }`.

---

### 1.4 Create JobRecord class

- [ ] Create `src/AsyncEndpoints.Abstractions/Jobs/JobRecord.cs`
- [ ] Properties:
  - `JobId` (Guid)
  - `JobName` (string)
  - `Channel` (string)
  - `Priority` (int)
  - `Partition` (int?)
  - `Payload` (string)
  - `Status` (JobStatus)
  - `RetryCount` (int)
  - `MaxRetries` (int)
  - `CreatedAt` (DateTime)
  - `StartedAt` (DateTime?)
  - `CompletedAt` (DateTime?)
  - `WorkerId` (string?)
  - `LastHeartbeat` (DateTime?)
  - `Result` (string?)
  - `ErrorMessage` (string?)
  - `Metadata` (Dictionary<string, string>?)
- [ ] Pure POCO — no methods, no logic

**Validation**: `JobRecord` compiles. Can serialize/deserialize with `System.Text.Json`.

---

### 1.5 Create IJobHandler<T> interface

- [ ] Create `src/AsyncEndpoints.Abstractions/Jobs/IJobHandler.cs`
- [ ] Define: `public interface IJobHandler<in TJob> { Task HandleAsync(TJob job, CancellationToken ct); }`

**Validation**: Interface compiles. A class can implement `IJobHandler<MyRequest>`.

---

### 1.6 Create IJobStore interface

- [ ] Create `src/AsyncEndpoints.Abstractions/Storage/IJobStore.cs`
- [ ] Define methods:
  - `Task<Guid> EnqueueAsync(JobDescriptor descriptor, CancellationToken ct)`
  - `Task<JobRecord?> DequeueAsync(string channel, IReadOnlySet<int>? partitions, CancellationToken ct)`
  - `Task UpdateStatusAsync(Guid jobId, JobStatus status, string? result, CancellationToken ct)`
  - `Task<JobRecord?> GetStatusAsync(Guid jobId, CancellationToken ct)`
  - `Task HeartbeatAsync(Guid jobId, CancellationToken ct)`
  - `Task<int> ReclaimStaleJobsAsync(TimeSpan staleTimeout, CancellationToken ct)`

**Validation**: Interface compiles. All methods are `Task`-returning. No `HttpContext` or ASP.NET types anywhere.

---

### 1.7 Create IJobListener interface

- [ ] Create `src/AsyncEndpoints.Abstractions/Listener/IJobListener.cs`
- [ ] Define: `Task<JobRecord?> WaitForNextJobAsync(string channel, IReadOnlySet<int>? partitions, CancellationToken ct)`

**Validation**: Interface compiles. No ASP.NET types.

---

### 1.8 Create IJobSubmitter interface

- [ ] Create `src/AsyncEndpoints.Abstractions/Submission/IJobSubmitter.cs`
- [ ] Define: `Task<Guid> SubmitAsync<T>(T job, string? channel = null, string? partitionKey = null, CancellationToken ct = default)`

**Validation**: Interface compiles. Generic. No ASP.NET types.

---

### 1.9 Create IPartitionAssigner interface

- [ ] Create `src/AsyncEndpoints.Abstractions/Partitioning/IPartitionAssigner.cs`
- [ ] Define:
  - `Task<int> AcquirePartitionAsync(string workerId, string channel, CancellationToken ct)`
  - `Task RenewLeaseAsync(int partition, string workerId, CancellationToken ct)`
  - `Task ReleasePartitionAsync(int partition, string workerId, CancellationToken ct)`

**Validation**: Interface compiles. No ASP.NET types.

---

### 1.10 Refactor Result<T> (was MethodResult)

- [ ] Create `src/AsyncEndpoints.Abstractions/Common/Result.cs`
- [ ] Define as a simple discriminated union:
  - `Result<T>.Success(T Value)`
  - `Result<T>.Failure(string Error)`
- [ ] Remove HTTP-specific error fields from the old `MethodResult`
- [ ] Keep old `MethodResult` as a deprecated wrapper if needed for backward compat

**Validation**: `Result<T>` compiles. Can pattern-match on success/failure. No `HttpContext` or ASP.NET types.

---

### 1.11 Review and keep existing utility types

- [ ] Review `AsyncEndpointError.cs` → rename to `JobError.cs` if appropriate, or keep as-is
- [ ] Keep `ExceptionInfo.cs` and `InnerExceptionInfo.cs` as-is (no ASP.NET deps)
- [ ] Verify no ASP.NET types are used in any remaining `Common/` files

**Validation**: All remaining types in `Abstractions/Common/` compile with zero dependencies.

---

### 1.12 Write unit tests for new abstractions

- [ ] Create unit tests in `tests/AsyncEndpoints.Abstractions.UnitTests/`:
  - [ ] `JobStatus` enum has all expected values
  - [ ] `JobDescriptor` can be created
  - [ ] `JobRecord` can be created and serialized
  - [ ] `Result<T>` success/failure pattern works
  - [ ] All interfaces are public and have correct signatures (reflection-based verification)

**Validation**: `dotnet test tests/AsyncEndpoints.Abstractions.UnitTests/` passes.

---

## Phase 01 Definition of Done

- [ ] `Abstractions.csproj` has **zero** package dependencies
- [ ] All 6 new interfaces compile: `IJobStore`, `IJobListener`, `IJobSubmitter`, `IJobHandler<T>`, `IPartitionAssigner`
- [ ] `JobDescriptor` and `JobRecord` replace the conceptual role of old `Job` class
- [ ] `JobStatus` includes `Processing` and `DeadLettered`
- [ ] `Result<T>` exists in simplified form
- [ ] No ASP.NET `using` statements exist anywhere in `Abstractions/`
- [ ] `dotnet build src/AsyncEndpoints.Abstractions/` succeeds
- [ ] Unit tests pass for all new types

**Next phase**: [Phase 02: Core ASP.NET Decoupling](phase-02-core-decoupling.md)
