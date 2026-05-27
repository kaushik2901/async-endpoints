# Phase 08: Provider Renaming and Cleanup

**Goal**: Align provider naming convention to `AsyncEndpoints.Provider.*` and fix project references.

**Prerequisites**: Phase 04 (IJobStore implementations stable and tested)

---

## Tasks

### 8.1 Rename Redis provider folder and project

- [ ] Rename folder: `src/AsyncEndpoints.Redis` → `src/AsyncEndpoints.Provider.Redis`
- [ ] Rename `.csproj` file to `AsyncEndpoints.Provider.Redis.csproj`
- [ ] Update the `.csproj` contents:
  - [ ] `<ProjectName>` and `<AssemblyName>` → `AsyncEndpoints.Provider.Redis`
  - [ ] `<RootNamespace>` → `AsyncEndpoints.Provider.Redis`
  - [ ] Update project reference from old aggregator → `Abstractions`
- [ ] Update all namespace declarations in Redis `.cs` files:
  - [ ] `namespace AsyncEndpoints.Redis.*` → `namespace AsyncEndpoints.Provider.Redis.*`
- [ ] Update solution file (`.sln`) to reference the new project path
- [ ] Ensure all `using` statements in other projects point to new namespace

**Validation**: `dotnet build src/AsyncEndpoints.Provider.Redis/` succeeds.

---

### 8.2 Update InMemory provider namespaces

- [ ] Update namespace declarations in all InMemory `.cs` files:
  - [ ] `namespace AsyncEndpoints.Provider.InMemory.*` (already correct if created in Phase 04)
  - [ ] If not: rename from `AsyncEndpoints.Provider.InMemory.*`
- [ ] Update `.csproj` to reference `Abstractions` (done in Phase 04)
- [ ] Ensure folder structure matches: `Storage/`, `DependencyInjection/`

**Validation**: `dotnet build src/AsyncEndpoints.Provider.InMemory/` succeeds.

---

### 8.3 Update project references in dependent projects

- [ ] Update `src/AsyncEndpoints.Worker/AsyncEndpoints.Worker.csproj`:
  - [ ] Ensure it references `Core` and `Abstractions` (not individual providers)
- [ ] Update `src/AsyncEndpoints.AspNetCore/AsyncEndpoints.AspNetCore.csproj`:
  - [ ] Ensure it references `Core` and `Abstractions`
- [ ] Update example projects:
  - [ ] `examples/RedisExampleAPI/` → reference `AsyncEndpoints.Provider.Redis` (new name)
  - [ ] `examples/RedisExampleWorker/` → reference `AsyncEndpoints.Provider.Redis` (new name)
  - [ ] `examples/InMemoryExampleAPI/` → reference `AsyncEndpoints.Provider.InMemory`

**Validation**: `dotnet build` succeeds for all projects.

---

### 8.4 Update test project references

- [ ] Update `tests/AsyncEndpoints.Redis.UnitTests/AsyncEndpoints.Redis.UnitTests.csproj`:
  - [ ] Reference `AsyncEndpoints.Provider.Redis` (new name)
  - [ ] Update namespace in test files if needed
- [ ] If renaming test project folder: `AsyncEndpoints.Redis.UnitTests` → `AsyncEndpoints.Provider.Redis.UnitTests`
- [ ] Update solution file with renamed test project

**Validation**: `dotnet test tests/AsyncEndpoints.Redis.UnitTests/` (or new name) passes.

---

### 8.5 Verify folder convention compliance

- [ ] Verify `src/AsyncEndpoints.Provider.InMemory/` structure:
  - [ ] `Storage/` — contains `InMemoryJobStore.cs`
  - [ ] `DependencyInjection/` — contains `ServiceCollectionExtensions.cs`
  - [ ] No other top-level folders except `Configuration/` if needed
- [ ] Verify `src/AsyncEndpoints.Provider.Redis/` structure:
  - [ ] `Storage/` — contains `RedisJobStore.cs`
  - [ ] `DependencyInjection/` — contains `ServiceCollectionExtensions.cs`
  - [ ] `Configuration/` — contains `RedisConfiguration.cs`
  - [ ] `Internal/` — contains `JobHashConverter.cs`, `RedisLuaScriptService.cs`

**Validation**: Both providers follow consistent folder conventions.

---

## Phase 08 Definition of Done

- [ ] Redis provider renamed to `AsyncEndpoints.Provider.Redis` (folder, csproj, namespaces)
- [ ] InMemory provider uses `AsyncEndpoints.Provider.InMemory` namespace
- [ ] All `.csproj` files reference `Abstractions` (not `Core` or aggregator directly) where appropriate
- [ ] Example projects reference new provider package names
- [ ] Solution file updated with new project paths
- [ ] `dotnet build` succeeds for entire solution
- [ ] Contract tests still pass for both providers

**Next phase**: [Phase 09: Aggregator Transition + Backward Compat](phase-09-aggregator-compat.md)
