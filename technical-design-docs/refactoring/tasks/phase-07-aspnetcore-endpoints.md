# Phase 07: AspNetCore Endpoint Rewrite

**Goal**: Clean AspNetCore endpoints using new submission flow (`IJobSubmitter`).

**Prerequisites**: Phase 01 (interfaces), Phase 02 (Core decoupled), Phase 05 (JobSubmitter ready)

---

## Tasks

### 7.1 Create JobEndpoints (POST /jobs)

- [ ] Create `src/AsyncEndpoints.AspNetCore/Endpoints/JobEndpoints.cs`
- [ ] Map `POST /jobs`:
  - [ ] Accept JSON body (generic `object` or `JsonElement`)
  - [ ] Read `JobName` from URL, header, or body field
  - [ ] Read `Channel` from query/header (optional, default "default")
  - [ ] Read `PartitionKey` from header (optional)
  - [ ] Call `IJobSubmitter.SubmitAsync(jobPayload, channel, partitionKey, ct)`
  - [ ] Return `202 Accepted` with `{ "jobId": "<guid>" }` response
  - [ ] Return `400 Bad Request` on validation failure
- [ ] Use `Microsoft.AspNetCore.Http.HttpResult` types (e.g., `Results.Accepted`, `Results.BadRequest`)

**Validation**: `POST /jobs` with valid body returns `202 Accepted` with a `JobId`. Invalid body returns `400`.

---

### 7.2 Create JobStatusEndpoint (GET /jobs/{id})

- [ ] Create `src/AsyncEndpoints.AspNetCore/Endpoints/JobStatusEndpoint.cs`
- [ ] Map `GET /jobs/{jobId}`:
  - [ ] Parse `jobId` as `Guid`
  - [ ] Call `IJobStore.GetStatusAsync(jobId, ct)`
  - [ ] If found → return `200 OK` with status DTO
  - [ ] If not found → return `404 Not Found`
- [ ] Response DTO: `{ jobId, status, result, errorMessage, createdAt, completedAt }`

**Validation**: `GET /jobs/{id}` returns correct status for existing job, `404` for unknown.

---

### 7.3 Create JobResultEndpoint (GET /jobs/{id}/result) — optional

- [ ] Create `src/AsyncEndpoints.AspNetCore/Endpoints/JobResultEndpoint.cs` (if needed)
- [ ] Map `GET /jobs/{jobId}/result`:
  - [ ] Return the result payload when job is completed
  - [ ] Return `424 (Failed Dependency)` or `409 (Conflict)` if not yet completed

**Validation**: Returns result on completed job, appropriate status otherwise.

---

### 7.4 Create EndpointRouteBuilderExtensions

- [ ] Create `src/AsyncEndpoints.AspNetCore/Extensions/EndpointRouteBuilderExtensions.cs`
- [ ] Define `MapAsyncEndpointsEndpoints(this IEndpointRouteBuilder routes, string prefix = "/jobs")`:
  - [ ] Map `POST {prefix}` → `JobEndpoints`
  - [ ] Map `GET {prefix}/{jobId}` → `JobStatusEndpoint`
  - [ ] Map `GET {prefix}/{jobId}/result` → `JobResultEndpoint` (if created)

**Validation**:
```csharp
app.MapAsyncEndpointsEndpoints("/jobs");
// Equivalent to:
// app.MapPost("/jobs", ...)
// app.MapGet("/jobs/{jobId}", ...)
// app.MapGet("/jobs/{jobId}/result", ...)
```

---

### 7.5 Remove old handler-based routing (or keep as shim)

- [ ] Review old routing: `MapAsyncPost<T>`, `MapAsyncPut<T>`, `MapAsyncGetJobDetails`, etc. in `RouteBuilderExtensions.cs`
- [ ] Either:
  - [ ] **Option A**: Remove entirely — consumers migrate to new endpoint pattern
  - [ ] **Option B**: Keep as deprecated shim that calls new services internally
- [ ] If keeping: mark all old methods `[Obsolete("Use MapAsyncEndpointsEndpoints instead")]`

**Validation**: Old routing either removed or marked obsolete. New routing is the primary path.

---

### 7.6 Update AspNetCore ServiceCollectionExtensions

- [ ] Update `src/AsyncEndpoints.AspNetCore/DependencyInjection/ServiceCollectionExtensions.cs`
- [ ] Remove worker and provider registrations (those belong in their respective packages)
- [ ] Register only AspNetCore-specific services:
  - [ ] Endpoint route builder extensions (no registration needed — static extension methods)
  - [ ] Any AspNetCore-specific configuration
- [ ] Define `AddAsyncEndpointsAspNetCore(this IServiceCollection services)` if needed

**Validation**: `AddAsyncEndpointsAspNetCore()` (or equivalent) registers only AspNetCore-layer services.

---

### 7.7 Create or update AspNetCoreOptions

- [ ] Create `src/AsyncEndpoints.AspNetCore/Configuration/AspNetCoreOptions.cs` (if not already done in Phase 03)
- [ ] Properties:
  - [ ] `EndpointPrefix` (string, default `"/jobs"`)
  - [ ] `DefaultResponseContentType` (string, default `"application/json"`)
  - [ ] Any HTTP-specific configuration

**Validation**: `AspNetCoreOptions` compiles and has sensible defaults.

---

### 7.8 Write integration tests for endpoints

- [ ] In `tests/AsyncEndpoints.AspNetCore.UnitTests/`:
  - [ ] Use `WebApplicationFactory` or `Microsoft.AspNetCore.TestHost` for integration tests
  - [ ] `POST /jobs — Returns 202 Accepted with JobId`
  - [ ] `POST /jobs — Returns 400 for invalid payload`
  - [ ] `POST /jobs — Uses specified channel`
  - [ ] `GET /jobs/{id} — Returns 200 with status when job exists`
  - [ ] `GET /jobs/{id} — Returns 404 when job not found`
  - [ ] `GET /jobs/{jobId}/result — Returns result when completed`

**Validation**: `dotnet test tests/AsyncEndpoints.AspNetCore.UnitTests/` passes.

---

## Phase 07 Definition of Done

- [ ] `POST /jobs` endpoint exists and returns `202 Accepted`
- [ ] `GET /jobs/{id}` endpoint exists and returns job status
- [ ] `GET /jobs/{id}/result` endpoint exists (optional)
- [ ] `MapAsyncEndpointsEndpoints(prefix)` extension method maps all routes
- [ ] Old handler-based routing either removed or marked `[Obsolete]`
- [ ] AspNetCore DI registration clean — only AspNetCore-specific services
- [ ] `AspNetCoreOptions` exists with endpoint configuration
- [ ] `dotnet build` succeeds on AspNetCore project
- [ ] Integration tests pass for all endpoints

**Next phase**: [Phase 08: Provider Renaming and Cleanup](phase-08-provider-renaming.md)
