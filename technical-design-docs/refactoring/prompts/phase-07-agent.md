# Agent Prompt: Phase 07 — AspNetCore Endpoint Rewrite

## Role
You are implementing Phase 07 of the AsyncEndpoints architecture realignment. You rewrite the ASP.NET Core endpoints to use the new `IJobSubmitter` submission flow.

## Key Architectural Context
New endpoints:
- `POST /jobs` → accept JSON body → `IJobSubmitter.SubmitAsync` → return `202 Accepted { jobId }`
- `GET /jobs/{id}` → `IJobStore.GetStatusAsync` → return `200 OK` with status or `404`
- `GET /jobs/{id}/result` → return job result if completed

Old handler-based routing (`MapAsyncPost<T>`, `MapAsyncPut<T>`, `IAsyncEndpointRequestDelegate`) is being replaced by clean Minimal API endpoints via `MapAsyncEndpointsEndpoints("/jobs")`.

## Current State Before Phase
- Core orchestration (JobSubmitter) exists (Phase 05)
- Core has no ASP.NET deps (Phase 02)
- Old `RouteBuilderExtensions.cs` still has old handler-based routing
- Old `AsyncEndpointRequestDelegate.cs` exists
- Old `IAsyncEndpointRequestDelegate.cs` exists

## Phase Goal
Create new Minimal API endpoints. Provide `MapAsyncEndpointsEndpoints()` extension. Clean up AspNetCore DI registration.

## Task List

### 7.1 Create `AspNetCore/Endpoints/JobEndpoints.cs`
- `POST /jobs`: deserialize body → `IJobSubmitter.SubmitAsync` → `Results.Accepted()` with `{ jobId }`
- Support `?channel=` query param and `X-Channel` header
- Return `400` on invalid body

### 7.2 Create `AspNetCore/Endpoints/JobStatusEndpoint.cs`
- `GET /jobs/{jobId}`: parse Guid → `IJobStore.GetStatusAsync` → `200 OK` with status DTO or `404`

### 7.3 Create `AspNetCore/Endpoints/JobResultEndpoint.cs` (optional)
- `GET /jobs/{jobId}/result`: return result when completed

### 7.4 Create `AspNetCore/Extensions/EndpointRouteBuilderExtensions.cs`
- `MapAsyncEndpointsEndpoints(this IEndpointRouteBuilder, string prefix = "/jobs")`: maps all 3 routes

### 7.5 Handle old routing
- Either delete `RouteBuilderExtensions.cs` (old `MapAsyncPost<T>` etc.) or mark all methods `[Obsolete]`

### 7.6 Update `AspNetCore/DependencyInjection/ServiceCollectionExtensions.cs`
- Remove worker/provider registrations — only register AspNetCore-specific services

### 7.7 Create/update `AspNetCore/Configuration/AspNetCoreOptions.cs` (if not done in Phase 03)
- Properties: `EndpointPrefix` ("/jobs"), `DefaultResponseContentType` ("application/json")

### 7.8 Write integration tests using `WebApplicationFactory`

## Validation
- `POST /jobs` returns `202 Accepted` with `jobId`
- `GET /jobs/{id}` returns `200` with status or `404`
- `MapAsyncEndpointsEndpoints()` works as an extension method
- `dotnet build src/AsyncEndpoints.AspNetCore/` succeeds
- `dotnet test tests/AsyncEndpoints.AspNetCore.UnitTests/` passes

## Do NOT
- Modify Worker (already done)
- Modify provider implementations (already done)
- Modify Abstractions or Core
