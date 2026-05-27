# Agent Prompt: Phase 08 — Provider Renaming and Cleanup

## Role
You are implementing Phase 08 of the AsyncEndpoints architecture realignment. You rename the Redis provider and align provider namespaces/folders to the `AsyncEndpoints.Provider.*` convention.

## Key Architectural Context
Target provider naming: `AsyncEndpoints.Provider.InMemory`, `AsyncEndpoints.Provider.Redis`, `AsyncEndpoints.Provider.SqlServer`, `AsyncEndpoints.Provider.Postgres`

Current: `AsyncEndpoints.Provider.InMemory` (mostly correct), `AsyncEndpoints.Redis` (wrong prefix, wrong folder name)

Provider folder convention: `Storage/`, `DependencyInjection/`, optionally `Configuration/` and `Internal/`

## Current State Before Phase
- Redis project: folder `src/AsyncEndpoints.Redis`, namespace `AsyncEndpoints.Redis.*`, references Aggregator (should be Abstractions)
- InMemory project: already `AsyncEndpoints.Provider.InMemory` namespace but may need folder structure cleanup
- Both providers compile and implement new IJobStore (Phase 04)

## Phase Goal
Rename Redis to `AsyncEndpoints.Provider.Redis`. Ensure both providers follow the folder convention. Fix all project references.

## Task List

### 8.1 Rename Redis folder and project
- Move `src/AsyncEndpoints.Redis/` → `src/AsyncEndpoints.Provider.Redis/`
- Rename `.csproj` → `AsyncEndpoints.Provider.Redis.csproj`
- Update csproj: `ProjectName`, `AssemblyName`, `RootNamespace` → `AsyncEndpoints.Provider.Redis`
- Update all namespaces in `.cs` files: `AsyncEndpoints.Redis.*` → `AsyncEndpoints.Provider.Redis.*`
- Update solution file to reference new path
- Update all `using` statements across the solution

### 8.2 Fix InMemory namespaces (if needed)
- Ensure namespace is `AsyncEndpoints.Provider.InMemory.*`
- Ensure folder structure: `Storage/`, `DependencyInjection/`

### 8.3 Update all project references
- `examples/RedisExampleAPI/` → reference `AsyncEndpoints.Provider.Redis`
- `examples/RedisExampleWorker/` → reference `AsyncEndpoints.Provider.Redis`
- `examples/InMemoryExampleAPI/` → reference `AsyncEndpoints.Provider.InMemory`
- Test projects → reference new names

### 8.4 Update test projects
- Rename `tests/AsyncEndpoints.Redis.UnitTests/` if needed → `AsyncEndpoints.Provider.Redis.UnitTests`
- Fix namespaces and references in test files

## Validation
- `dotnet build` succeeds for entire solution
- `dotnet test` passes for all test projects
- Both providers follow `Storage/` and `DependencyInjection/` folder convention
- No references to old `AsyncEndpoints.Redis.*` namespaces remain

## Do NOT
- Change any implementation logic
- Modify Core, Worker, or AspNetCore orchestration
