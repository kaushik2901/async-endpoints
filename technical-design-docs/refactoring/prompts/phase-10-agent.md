# Agent Prompt: Phase 10 — Examples, Docs, and Samples Update

## Role
You are implementing Phase 10 of the AsyncEndpoints architecture realignment. You update all example projects to use the new API pattern and update documentation.

## Key Architectural Context
**Old handler pattern** (being replaced):
```csharp
class MyHandler : IAsyncEndpointRequestHandler<MyRequest, MyResponse> {
    Task<MethodResult<MyResponse>> HandleAsync(AsyncContext<MyRequest> ctx, CancellationToken ct);
}
```

**New handler pattern**:
```csharp
class MyHandler : IJobHandler<MyRequest> {
    Task HandleAsync(MyRequest job, CancellationToken ct);
}
```

**Old DI**:
```csharp
services.AddAsyncEndpoints().AddAsyncEndpointsInMemoryStore().AddAsyncEndpointsWorker().AddAsyncEndpointHandler<...>();
app.MapAsyncPost<MyRequest>("my-job", "/api/my-job");
```

**New DI**:
```csharp
services.AddAsyncEndpointsCore().UseInMemory().AddAsyncEndpointsWorker().AddJobHandler<MyHandler, MyRequest>();
app.MapAsyncEndpointsEndpoints("/jobs");
```

## Current State Before Phase
- Example projects still use old API: old handler interfaces, old DI registration, old routing
- `examples/InMemoryExampleAPI/` — old pattern
- `examples/RedisExampleAPI/` — old pattern, references old package name
- `examples/RedisExampleWorker/` — old pattern
- `README.md` still documents old API

## Phase Goal
Update all 3 example projects to new API. Update README.

## Task List

### 10.1 Update `examples/InMemoryExampleAPI/`
- `Program.cs`: use `AddAsyncEndpointsCore().UseInMemory().AddAsyncEndpointsWorker().AddJobHandler<H, T>()`
- `app.MapAsyncEndpointsEndpoints("/jobs")` instead of `MapAsyncPost<T>`
- Handler: implement `IJobHandler<MyRequest>` instead of `IAsyncEndpointRequestHandler<...>`
- Remove `AsyncContext` usage — work with `MyRequest` directly
- `.csproj`: reference `AsyncEndpoints.Provider.InMemory` (not Aggregator)

### 10.2 Update `examples/RedisExampleAPI/`
- Same pattern as InMemory but with Redis provider
- `.csproj`: reference `AsyncEndpoints.Provider.Redis`

### 10.3 Update `examples/RedisExampleWorker/`
- Worker-only: no HTTP endpoints, just `AddAsyncEndpointsCore().UseRedis().AddAsyncEndpointsWorker().AddJobHandler<H, T>()`

### 10.4 Update `README.md`
- New package names table
- Quick-start snippet with new API
- Migration guide link
- No old API code snippets

## Validation
- All 3 example projects compile and run
- InMemoryExampleAPI: can POST a job and see it processed (verify via curl)
- RedisExampleWorker: starts and processes jobs from Redis
- README examples match the actual API

## Do NOT
- Change any library code (Core, Worker, AspNetCore, providers)
- Introduce new features beyond what examples demonstrate
