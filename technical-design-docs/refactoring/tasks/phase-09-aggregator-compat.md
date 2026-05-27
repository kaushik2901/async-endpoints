# Phase 09: Aggregator Transition + Backward Compatibility

**Goal**: Keep `AsyncEndpoints` (meta package) as a compatibility shim during transition. Mark old APIs as `[Obsolete]`.

**Prerequisites**: Phase 02 (Core decoupled), Phase 05 (Core orchestration), Phase 06 (Worker), Phase 07 (AspNetCore), Phase 08 (Providers renamed)

---

## Tasks

### 9.1 Rewrite Aggregator ServiceCollectionExtensions as shim

- [ ] Open `src/AsyncEndpoints/Extensions/ServiceCollectionExtensions.cs`
- [ ] Rewrite to forward all calls to new individual package registrations:
  - [ ] `AddAsyncEndpoints(Action<AsyncEndpointsConfigurations>?)` → calls `AddAsyncEndpointsCore()`
  - [ ] `AddAsyncEndpointsWorker(Action<AsyncEndpointsWorkerConfigurations>?)` → calls `AddAsyncEndpointsWorker()`
  - [ ] `AddAsyncEndpointsInMemoryStore()` → calls `AddAsyncEndpointsInMemory()`
- [ ] Mark **all** methods with `[Obsolete("Use the specific package registration methods instead. See migration guide at ..."))]`
- [ ] Update old parameter types to convert to new options types internally

**Validation**: `services.AddAsyncEndpoints()` compiles with an `[Obsolete]` warning. Calling it produces the same registration as the new approach.

---

### 9.2 Update Aggregator .csproj

- [ ] Open `src/AsyncEndpoints/AsyncEndpoints.csproj`
- [ ] Keep all current project references (for backward compat):
  - [ ] `Abstractions`
  - [ ] `Core`
  - [ ] `Worker`
  - [ ] `AspNetCore`
  - [ ] `Provider.InMemory`
- [ ] Ensure `Provider.Redis` is **not** a mandatory reference (must be added explicitly by consumers)
- [ ] Add `<PackageId>AsyncEndpoints</PackageId>` if not present
- [ ] Add NuGet metadata: `Summary`, `Description`, `Tags` indicating this is a legacy meta package

**Validation**: Aggregator compiles. `[Obsolete]` warnings appear when using old API.

---

### 9.3 Add [Obsolete] on old interfaces (if keeping for transition)

- [ ] If keeping old `IAsyncEndpointRequestHandler<TRequest, TResponse>` for transition:
  - [ ] Mark it `[Obsolete("Implement IJobHandler<T> instead")]`
- [ ] If keeping old `IJobManager`:
  - [ ] Mark it `[Obsolete("Use IJobSubmitter + IJobStore directly")]`
- [ ] If keeping old `IJobRecoveryService`:
  - [ ] Mark it `[Obsolete("Stale job reclaim is now part of IJobStore")]`

**Validation**: Old interface usage produces clear `[Obsolete]` warnings pointing to the replacement.

---

### 9.4 Create adapter for old handler interface (if needed)

- [ ] Create `src/AsyncEndpoints.AspNetCore/Adapters/AsyncEndpointRequestHandlerAdapter.cs` (optional):
  - [ ] Implements `IJobHandler<TRequest>` by wrapping old `IAsyncEndpointRequestHandler<TRequest, TResponse>`
  - [ ] Allows consumers to migrate handlers incrementally
- [ ] Mark adapter as `[Obsolete]` too — it's a transition aid only

**Validation**: Old handler implementations work through the adapter with `[Obsolete]` warnings.

---

### 9.5 Update NuGet package metadata

- [ ] Ensure all `.csproj` files have correct:
  - [ ] `PackageId` (new naming: `AsyncEndpoints.Core`, `AsyncEndpoints.Worker`, etc.)
  - [ ] `Description`
  - [ ] `Tags`
  - [ ] `Version` (aligned — all packages should be the same version)
- [ ] Aggregator.csproj: add `<DeprecationMessage>` or documentation pointing to new packages

**Validation**: `dotnet pack` produces packages with correct metadata.

---

## Phase 09 Definition of Done

- [ ] Aggregator `ServiceCollectionExtensions` forwards all calls to new packages
- [ ] All old methods marked `[Obsolete]` with clear migration message
- [ ] Aggregator `.csproj` references all packages (except Redis) as meta package
- [ ] `IAsyncEndpointRequestHandler` and `IJobManager` have `[Obsolete]` attributes (if kept)
- [ ] Adapter exists for old → new handler transition (optional)
- [ ] NuGet metadata is correct for all packages
- [ ] Existing consumers using `AsyncEndpoints` can still compile (with warnings)
- [ ] `dotnet build` succeeds

**Next phase**: [Phase 10: Examples, Docs, and Samples Update](phase-10-examples-docs.md)
