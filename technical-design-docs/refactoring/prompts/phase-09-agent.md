# Agent Prompt: Phase 09 — Aggregator Transition + Backward Compatibility

## Role
You are implementing Phase 09 of the AsyncEndpoints architecture realignment. You convert the `AsyncEndpoints` meta package into a backward-compatibility shim that forwards calls to the new individual packages.

## Key Architectural Context
The old API used a single `AsyncEndpoints` NuGet package with all registrations:
```csharp
services.AddAsyncEndpoints().AddAsyncEndpointsInMemoryStore().AddAsyncEndpointsWorker().AddAsyncEndpointHandler<...>();
```

The new API uses individual packages:
```csharp
services.AddAsyncEndpointsCore().UseInMemory().AddAsyncEndpointsWorker().AddJobHandler<...>();
```

The meta package must:
- Forward all old methods to new package methods
- Mark all old methods `[Obsolete]`
- Keep compiling for existing consumers during transition

## Current State Before Phase
- All new services built and registered in their respective packages (Phases 05, 06, 07)
- Providers renamed and working (Phase 08)
- Old aggregator `ServiceCollectionExtensions.cs` still has original code
- Old interfaces (`IAsyncEndpointRequestHandler`, `IJobManager`) may still exist

## Phase Goal
Rewire aggregator as compatibility shim. Mark old APIs `[Obsolete]`. Optionally create handler adapters.

## Task List

### 9.1 Rewrite `Aggregator/Extensions/ServiceCollectionExtensions.cs`
- Replace all implementations with forwarding calls to new packages
- Example: `AddAsyncEndpoints()` → calls `services.AddAsyncEndpointsCore()`
- Example: `AddAsyncEndpointsInMemoryStore()` → calls `services.AddAsyncEndpointsInMemory()`
- Mark ALL methods: `[Obsolete("Use specific package registrations: AddAsyncEndpointsCore(), AddAsyncEndpointsWorker(), AddAsyncEndpointsInMemory()")]`

### 9.2 Update Aggregator `.csproj`
- Keep references to all packages (Abstractions, Core, Worker, AspNetCore, InMemory)
- Do NOT include Redis as mandatory reference
- Add NuGet metadata noting this is a legacy meta package

### 9.3 Mark old interfaces as `[Obsolete]` (if they still exist)
- `IAsyncEndpointRequestHandler<TRequest, TResponse>` → `[Obsolete("Implement IJobHandler<T> instead")]`
- `IJobManager` → `[Obsolete("Use IJobSubmitter + IJobStore directly")]`

### 9.4 Create adapter (optional)
- `AspNetCore/Adapters/AsyncEndpointRequestHandlerAdapter.cs` — wraps old handler to implement `IJobHandler<T>`

### 9.5 Update NuGet package metadata on all `.csproj` files

## Validation
- `dotnet build` succeeds with only `[Obsolete]` warnings (no errors)
- Calling `services.AddAsyncEndpoints()` still works (forwards to new registrations)
- `[Obsolete]` message clearly points to the replacement API
- NuGet packages pack successfully

## Do NOT
- Modify any implementation logic in Core, Worker, AspNetCore, or providers
- Remove any old files that consumers might still reference
