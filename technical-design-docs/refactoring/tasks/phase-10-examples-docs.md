# Phase 10: Examples, Docs, and Samples Update

**Goal**: Update all example projects to use the new API pattern. Update documentation.

**Prerequisites**: Phase 05 (Core orchestration), Phase 07 (AspNetCore endpoints), Phase 08 (Provider renaming)

---

## Tasks

### 10.1 Update InMemoryExampleAPI

- [ ] Open `examples/InMemoryExampleAPI/Program.cs`
- [ ] Update to new API pattern:
  - [ ] `AddAsyncEndpointsCore()` instead of old aggregator
  - [ ] `.UseInMemory()` instead of `AddAsyncEndpointsInMemoryStore()`
  - [ ] `AddAsyncEndpointsWorker()` with worker options
  - [ ] `AddJobHandler<MyHandler, MyRequest>()` instead of `AddAsyncEndpointHandler<MyHandler, MyRequest, MyResponse>()`
  - [ ] `app.MapAsyncEndpointsEndpoints("/jobs")` instead of `app.MapAsyncPost<T>()`, `app.MapAsyncGetJobDetails()`
- [ ] Update handler implementation:
  - [ ] `IJobHandler<MyRequest>` instead of `IAsyncEndpointRequestHandler<MyRequest, MyResponse>`
  - [ ] `HandleAsync(MyRequest job, CancellationToken ct)` instead of `HandleAsync(AsyncContext<MyRequest> ctx, CancellationToken ct)`
  - [ ] Remove `AsyncContext` usage — work with the deserialized request directly
- [ ] Update `.csproj` references to new package names

**Validation**: `dotnet run` on the example works. Can POST a job and see it processed.

---

### 10.2 Update RedisExampleAPI

- [ ] Open `examples/RedisExampleAPI/Program.cs`
- [ ] Update to new API pattern (same as 10.1):
  - [ ] `AddAsyncEndpointsCore()`
  - [ ] `.UseRedis(connectionString)` or `AddAsyncEndpointsRedis(config)`
  - [ ] `AddAsyncEndpointsWorker()`
  - [ ] `AddJobHandler<MyHandler, MyRequest>()`
  - [ ] `app.MapAsyncEndpointsEndpoints("/jobs")`
- [ ] Update handler to `IJobHandler<T>` pattern
- [ ] Update `.csproj` to reference `AsyncEndpoints.Provider.Redis`

**Validation**: `dotnet run` on the example works with Redis backend.

---

### 10.3 Update RedisExampleWorker

- [ ] Open `examples/RedisExampleWorker/Program.cs`
- [ ] Update to new API pattern (worker-only — no HTTP endpoints):
  - [ ] `AddAsyncEndpointsCore()`
  - [ ] `.UseRedis(connectionString)`
  - [ ] `AddAsyncEndpointsWorker()` — worker polls and processes
  - [ ] `AddJobHandler<MyHandler, MyRequest>()`
- [ ] Update handler to `IJobHandler<T>` pattern
- [ ] Remove any remaining `IAsyncEndpointRequestHandler` references

**Validation**: Worker starts, polls Redis, and processes jobs.

---

### 10.4 Remove or update old example code

- [ ] Remove any old handler implementations that use `AsyncContext<T>` pattern
- [ ] Remove old inline registration examples (if any)
- [ ] Ensure all examples compile and run

**Validation**: All examples build and run. No old API usage remains.

---

### 10.5 Create/update README.md

- [ ] Update root `README.md`:
  - [ ] New package names and their purposes
  - [ ] Quick-start with the new API pattern
  - [ ] Migration guide for existing consumers (link to migration appendix)
  - [ ] Updated code snippets showing `IJobHandler<T>`, `IJobSubmitter`, etc.
  - [ ] Note about `[Obsolete]` warnings and transition period

**Validation**: README accurately reflects the new architecture.

---

### 10.6 Create samples directory (optional)

- [ ] Create `samples/` with standalone runnable projects if needed:
  - [ ] Minimal API sample (single file)
  - [ ] Advanced sample with channels and partitioning
  - [ ] Worker-only sample (no HTTP)

**Validation**: Sample projects build and run.

---

## Phase 10 Definition of Done

- [ ] `InMemoryExampleAPI` uses new API pattern and `IJobHandler<T>`
- [ ] `RedisExampleAPI` uses new API pattern and references `AsyncEndpoints.Provider.Redis`
- [ ] `RedisExampleWorker` uses new API pattern (worker-only)
- [ ] All examples compile and run
- [ ] No old API usage (`IAsyncEndpointRequestHandler`, `AsyncContext`, etc.) remains in examples
- [ ] `README.md` updated with new architecture and migration guide
- [ ] Samples created (optional)

**Next phase**: [Phase 11: Test Restructuring](phase-11-test-restructuring.md)
