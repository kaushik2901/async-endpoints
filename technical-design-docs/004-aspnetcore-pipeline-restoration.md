# Restoring the ASP.NET Core Integration Pipeline

**Date:** 2026-05-28  
**Status:** Draft  
**Drivers:** [Issue Analysis]

---

## 1. Problem Statement

The recent refactoring (phases 01-11) migrated from a legacy pipeline (based on `Job`, `IJobManager`, `IAsyncEndpointRequestHandler`) to a streamlined new pipeline (based on `JobRecord`, `JobDescriptor`, `IJobStore`, `IJobSubmitter`). However, five specific concerns were identified in the `AsyncEndpoints.AspNetCore` project:

### 1.1 Lost Per-Endpoint Handler Mapping

**Before refactoring:** `RouteBuilderExtensions` provided `MapAsyncPost<TRequest>("jobName", "/pattern")`, allowing developers to declare typed async endpoints where each URL pattern mapped to a specific `jobName` → handler.

**After refactoring:** A single `JobEndpoints.PostJob` endpoint handles ALL POST requests at `/jobs`. The `jobName` is inferred as `typeof(T).Name` which, when called with a `JsonElement` body, becomes `"JsonElement"` for every request — effectively breaking handler dispatch.

### 1.2 Lost HTTP Context in Job Payload

**Before refactoring:** `AsyncEndpointRequestDelegate` captured HTTP headers, route parameters, and query parameters from the `HttpContext` and stored them in the legacy `Job` object alongside the serialized body payload. Handlers accessed these via `AsyncContext<TRequest>`.

**After refactoring:** `JobEndpoints.PostJob` extracts only `channel` and `partitionKey` from the request. Headers, route params, and query params are discarded. Handlers have no way to access HTTP context information.

### 1.3 AOT Compatibility Gaps

- `JobEndpoints.PostJob` uses `JsonSerializer.Deserialize<JsonElement>()` directly instead of going through the AOT-safe `ISerializer` pipeline
- `JsonBodyParserService` calls `_serializer.DeserializeAsync<T>(stream, (JsonSerializerOptions?)null)` using the reflection fallback path
- Anonymous types used in response objects (e.g., `new { jobId, result }`) are unsupported in Native AOT scenarios
- The `AsyncEndpointsJsonSerializationContext` does not include the new `HttpJobPayload` type

### 1.4 Non-Standard Error Responses

Several endpoints return `Results.BadRequest(object)`, `Results.NotFound(object)`, or `Results.Conflict(object)` instead of the standard `Results.Problem(...)` which produces RFC 7807 Problem Details responses.

| Endpoint | Current | Required |
|----------|---------|----------|
| `JobEndpoints.PostJob` | `Results.BadRequest(new { error })` | `Results.Problem(detail, statusCode: 400)` |
| `JobStatusEndpoint.GetJobStatus` | `Results.NotFound(new { error })` | `Results.Problem(detail, statusCode: 404)` |
| `JobResultEndpoint.GetJobResult` | `Results.Conflict(new { ... })` | `Results.Problem(detail, statusCode: 409)` |

### 1.5 Lost Response Customization

`AsyncEndpointsResponseConfigurations` was marked `[Obsolete]` with no replacement in the new pipeline. This removed the ability for developers to override how job submission responses, status responses, and error responses are rendered.

---

## 2. Proposed Design

### 2.1 `HttpJobPayload` — Capturing HTTP Context

A new AOT-safe record type in `AsyncEndpoints.AspNetCore` that serializes the HTTP request context into the job payload string. The job payload in `JobDescriptor.Payload` is already a `string`; we embed this serialized payload inside it.

```
HTTP Request
  ├── Body (raw JSON)
  ├── Headers
  ├── Route Params
  └── Query Params
       │
       ▼
  HttpJobPayload (serialized to JSON string)
       │
       ▼
  IJobSubmitter.SubmitAsync("jobName", httpJobPayload)
       │
       ▼
  JobDescriptor { JobName, Payload = JsonSerializer.Serialize(httpJobPayload) }
       │
       ▼
  IJobStore.EnqueueAsync(descriptor)
```

```csharp
// src/AsyncEndpoints.AspNetCore/Models/HttpJobPayload.cs
namespace AsyncEndpoints.AspNetCore.Models;

public sealed record HttpJobPayload
{
    public string Body { get; init; } = string.Empty;
    public Dictionary<string, List<string?>>? Headers { get; init; }
    public Dictionary<string, string?>? RouteParams { get; init; }
    public Dictionary<string, List<string?>>? QueryParams { get; init; }
}
```

**Key decisions:**
- `Body` is stored as a raw JSON string (not a `JsonElement` or deserialized type) to preserve the original payload for AOT-safe deserialization on the worker side
- `RouteParams` uses `Dictionary<string, string?>` instead of `Dictionary<string, object?>` (legacy) because `object?` prevents source-generated serialization
- Type is a record — immutable by convention, supports `with` expressions

**Registration in `AsyncEndpointsJsonSerializationContext`:**
```csharp
[JsonSerializable(typeof(HttpJobPayload))]
public partial class AsyncEndpointsJsonSerializationContext { }
```

### 2.2 Per-Endpoint Route Registration

Replace the obsolete `RouteBuilderExtensions` with a new class `AsyncEndpointRouteBuilderExtensions` that provides typed, per-endpoint registration using the new pipeline.

**Design goals:**
- Each endpoint maps a URL pattern + HTTP method to a specific `jobName`
- Full HTTP context (headers, route params, query params, body) is captured
- Uses `IJobSubmitter` (not legacy `IJobManager`)
- Uses `ISerializer` for AOT-safe deserialization of request body
- Returns `IResult` using response configurations

**Extension methods:**

```csharp
public static class AsyncEndpointRouteBuilderExtensions
{
    // With body deserialization
    public static IEndpointConventionBuilder MapAsyncPost<TRequest>(
        this IEndpointRouteBuilder endpoints,
        string jobName,
        string pattern,
        Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null);

    // Without body
    public static IEndpointConventionBuilder MapAsyncPost(
        this IEndpointRouteBuilder endpoints,
        string jobName,
        string pattern,
        Func<HttpContext, CancellationToken, Task<IResult?>?>? handler = null);

    // PUT, PATCH, DELETE variants...
    public static IEndpointConventionBuilder MapAsyncPut<TRequest>(...);
    public static IEndpointConventionBuilder MapAsyncGetJobDetails(...);
}
```

**Internal flow:**

```
MapAsyncPost<CreateOrderRequest>("create-order", "/orders")
  ↓
routes.MapPost("/orders", async (HttpContext httpContext, ...) =>
{
    1. Deserialize body as TRequest using ISerializer (AOT-safe)
    2. Build HttpJobPayload {
        Body = rawBodyString,
        Headers = httpContext.GetHeadersFromContext(),
        RouteParams = httpContext.GetRouteParamsFromContext(),
        QueryParams = httpContext.GetQueryParamsFromContext()
      }
    3. Submit via IJobSubmitter.SubmitAsync("create-order", httpJobPayload)
    4. Return response via responseConfig.JobSubmittedResponseFactory(...)
})
```

**Handler flow (worker side):**

```
Handler registered for "create-order":
  HandlerRegistry.Register("create-order", async (sp, record, ct) =>
  {
      var payload = serializer.Deserialize<HttpJobPayload>(record.Payload, ...);
      var request = serializer.Deserialize<CreateOrderRequest>(payload.Body, ...);
      var handler = sp.GetRequiredService<IJobHandler<CreateOrderRequest>>();
      await handler.HandleAsync(request, ct);
      return null; // or a result string
  });
```

### 2.3 `IJobSubmitter` Enhancement

Add a `jobName` parameter overload to allow explicit job naming:

```csharp
public interface IJobSubmitter
{
    Task<Guid> SubmitAsync<T>(T job, string? channel = null, string? partitionKey = null, CancellationToken ct = default);
    Task<Guid> SubmitAsync<T>(string jobName, T job, string? channel = null, string? partitionKey = null, CancellationToken ct = default);
}
```

**Implementation:**

```csharp
public async Task<Guid> SubmitAsync<T>(string jobName, T job, string? channel = null, string? partitionKey = null, CancellationToken ct = default)
{
    var payload = SerializeJob(job);
    var descriptor = new JobDescriptor(
        JobName: jobName,                 // Use provided name
        Payload: payload,
        Channel: channel ?? "default",
        PartitionKey: partitionKey);
    return await _store.EnqueueAsync(descriptor, ct);
}
```

The original overload delegates to this with `jobName = typeof(T).Name` for backward compatibility.

### 2.4 `AsyncEndpointsResponseConfigurations` Revival

A new, non-obsolete version adapted for the new pipeline types:

```csharp
// src/AsyncEndpoints.AspNetCore/Configuration/AsyncEndpointsResponseConfigurations.cs
namespace AsyncEndpoints.AspNetCore.Configuration;

public sealed class AsyncEndpointsResponseConfigurations
{
    // Called after job is submitted successfully
    public Func<Guid, HttpContext, Task<IResult>> JobSubmittedResponseFactory { get; set; }

    // Called to render job status
    public Func<JobRecord?, HttpContext, Task<IResult>> JobStatusResponseFactory { get; set; }

    // Called to render job result
    public Func<JobRecord, HttpContext, Task<IResult>> JobResultResponseFactory { get; set; }

    // Called on exceptions
    public Func<Exception, HttpContext, Task<IResult>> ExceptionResponseFactory { get; set; }
}
```

**Default implementations** (in `ResponseDefaults`, updated):

```csharp
public static class ResponseDefaults
{
    public static Task<IResult> DefaultJobSubmittedResponseFactory(Guid jobId, HttpContext _)
        => Task.FromResult(Results.Accepted($"/jobs/{jobId}", new { jobId }));

    public static Task<IResult> DefaultJobStatusResponseFactory(JobRecord? record, HttpContext _)
    {
        if (record is null)
            return Task.FromResult(Results.Problem(
                detail: "Job not found", statusCode: 404));

        return Task.FromResult(Results.Ok(JobStatusResponseMapper.ToResponse(record)));
    }

    public static Task<IResult> DefaultExceptionResponseFactory(Exception ex, HttpContext _)
        => Task.FromResult(Results.Problem(
            detail: ex.Message, title: "An error occurred", statusCode: 500));
}
```

**AOT-safe response DTOs** replace anonymous types:

```csharp
// src/AsyncEndpoints.AspNetCore/Models/JobStatusResponse.cs
public sealed record JobStatusResponse
{
    public Guid JobId { get; init; }
    public string JobName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? Result { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int RetryCount { get; init; }
    public int MaxRetries { get; init; }
}

// src/AsyncEndpoints.AspNetCore/Models/JobResultResponse.cs
public sealed record JobResultResponse
{
    public Guid JobId { get; init; }
    public string? Result { get; init; }
}
```

### 2.5 AOT-Safe Serialization Across All Endpoints

**Principle:** Never use `JsonSerializer` directly. Always go through `ISerializer` with `JsonTypeInfo<T>` source-generated contexts.

**Changes required:**

| File | Current | Fix |
|------|---------|-----|
| `JobEndpoints.cs` | `JsonSerializer.Deserialize<JsonElement>()` | Replace with `ISerializer.DeserializeAsync<T>(stream, JsonTypeInfo)` 
| `JobStatusEndpoint.cs` | Anonymous types in `Results.Ok(new { ... })` | Use `JobStatusResponse` DTO registered in serialization context
| `JobResultEndpoint.cs` | Anonymous types in `Results.Ok/Conflict` | Use `JobResultResponse` DTO
| `JsonBodyParserService.cs` | Calls fallback path | Attempt AOT-safe path with type lookup, fall back only if necessary
| `AsyncEndpointsJsonSerializationContext.cs` | Missing `HttpJobPayload` and response DTOs | Add all new types

**AOT-safe deserialization pattern in endpoints:**

```csharp
// Try AOT-safe path first
var typeInfo = (JsonTypeInfo<TRequest>?)AsyncEndpointsJsonSerializationContext.Default.GetTypeInfo(typeof(TRequest));
var request = typeInfo is not null
    ? await serializer.DeserializeAsync(stream, typeInfo, ct)
    : await serializer.DeserializeAsync(stream, (JsonSerializerOptions?)null, ct);
```

### 2.6 Universal `Results.Problem` Adoption

Replace all non-success response patterns with RFC 7807 Problem Details:

| Endpoint | Status | Before | After |
|----------|--------|--------|-------|
| `PostJob` | 400 | `Results.BadRequest(new { error })` | `Results.Problem(detail, statusCode: 400)` |
| `PostJob` | 500 | `Results.Problem(detail, title, statusCode: 500)` | Already correct ✓ |
| `GetJobStatus` | 404 | `Results.NotFound(new { error })` | `Results.Problem(detail, statusCode: 404)` |
| `GetJobResult` | 404 | `Results.NotFound(new { error })` | `Results.Problem(detail, statusCode: 404)` |
| `GetJobResult` | 409 | `Results.Conflict(new { ... })` | `Results.Problem(detail, statusCode: 409)` |

---

## 3. Files to Create / Modify / Remove

### 3.1 New Files

| File | Purpose |
|------|---------|
| `src/AsyncEndpoints.AspNetCore/Models/HttpJobPayload.cs` | HTTP context payload record |
| `src/AsyncEndpoints.AspNetCore/Models/JobStatusResponse.cs` | AOT-safe status response DTO |
| `src/AsyncEndpoints.AspNetCore/Models/JobResultResponse.cs` | AOT-safe result response DTO |
| `src/AsyncEndpoints.AspNetCore/Models/JobSubmittedResponse.cs` | AOT-safe submission response DTO |
| `src/AsyncEndpoints.AspNetCore/Extensions/AsyncEndpointRouteBuilderExtensions.cs` | New per-endpoint registration |

### 3.2 Modified Files

| File | Changes |
|------|---------|
| `src/AsyncEndpoints.AspNetCore/Configuration/AsyncEndpointsResponseConfigurations.cs` | Remove `[Obsolete]`, adapt delegates for new types |
| `src/AsyncEndpoints.AspNetCore/Endpoints/ResponseDefaults.cs` | Remove `[Obsolete]`, adapt for new pipeline types |
| `src/AsyncEndpoints.AspNetCore/Endpoints/JobEndpoints.cs` | Use `ISerializer` with `JsonTypeInfo`, capture full HTTP context, use `Results.Problem` |
| `src/AsyncEndpoints.AspNetCore/Endpoints/JobStatusEndpoint.cs` | Use AOT-safe response DTO, use `Results.Problem` |
| `src/AsyncEndpoints.AspNetCore/Endpoints/JobResultEndpoint.cs` | Use AOT-safe response DTO, use `Results.Problem` |
| `src/AsyncEndpoints.AspNetCore/Serialization/JsonBodyParserService.cs` | Attempt AOT-safe deserialization first |
| `src/AsyncEndpoints.AspNetCore/Extensions/EndpointRouteBuilderExtensions.cs` | Add overloads accepting response configurations |
| `src/AsyncEndpoints.AspNetCore/Extensions/ServiceCollectionExtensions.cs` | Register `AsyncEndpointsResponseConfigurations` as scoped |
| `src/AsyncEndpoints.Core/Infrastructure/AsyncEndpointsJsonSerializationContext.cs` | Add `HttpJobPayload` and response DTO types |
| `src/AsyncEndpoints.Abstractions/Submission/IJobSubmitter.cs` | Add `jobName` overload |
| `src/AsyncEndpoints.Core/Submission/JobSubmitter.cs` | Implement `jobName` overload |

### 3.3 Removed or Kept as-Is

| File | Decision |
|------|----------|
| `RouteBuilderExtensions.cs` | Keep as `[Obsolete]` — replaced by `AsyncEndpointRouteBuilderExtensions` |
| `AsyncEndpointRequestDelegate.cs` | Keep as `[Obsolete]` — replaced by new flow |
| `IAsyncEndpointRequestDelegate.cs` | Keep as `[Obsolete]` — replaced by new flow |
| `JobResultResponse.cs` | Keep as `[Obsolete]` — replaced by `Models/JobResultResponse.cs` |
| `JobResponse.cs` / `JobResponseMapper.cs` | Keep as `[Obsolete]` — replaced by response DTOs |
| `IAsyncEndpointRequestHandler.cs` | Keep as `[Obsolete]` — replaced by `IJobHandler<T>` |

---

## 4. Handler Registration Pattern

To support the new per-endpoint flow, we need a way to register handlers that can deserialize `HttpJobPayload` and extract the typed request body. The existing `IHandlerRegistry` works with raw `(IServiceProvider, JobRecord, CancellationToken) => Task<string?>` delegates.

**Recommended pattern for developers:**

```csharp
// Program.cs
builder.Services.AddAsyncEndpointsCore();
builder.Services.AddAsyncEndpointsAspNetCore();
builder.Services.AddInMemoryStore();
builder.Services.AddScoped<IJobHandler<CreateOrderRequest>, CreateOrderHandler>();

var app = builder.Build();

app.MapAsyncEndpoint<CreateOrderRequest>("create-order")
   .MapPost("/orders");

app.Run();

// Handler registration (inside AddAsyncEndpointsCore or via extension)
HandlerRegistry.Register("create-order", async (sp, record, ct) =>
{
    var serializer = sp.GetRequiredService<ISerializer>();
    var handler = sp.GetRequiredService<IJobHandler<CreateOrderRequest>>();

    // Deserialize the HTTP context payload
    var httpPayload = serializer.Deserialize<HttpJobPayload>(
        record.Payload, AsyncEndpointsJsonSerializationContext.Default.HttpJobPayload);

    // Deserialize the actual request body
    var request = serializer.Deserialize<CreateOrderRequest>(
        httpPayload.Body, AsyncEndpointsJsonSerializationContext.Default.CreateOrderRequest ???);

    await handler.HandleAsync(request, ct);
    return null;
});
```

For a smoother developer experience, provide a generic registration helper:

```csharp
public static class HandlerRegistrationExtensions
{
    public static void RegisterJobHandler<TRequest>(
        this IHandlerRegistry registry,
        string jobName,
        Func<HttpJobPayload, TRequest>? bodyTransform = null)
    {
        registry.Register(jobName, async (sp, record, ct) =>
        {
            var serializer = sp.GetRequiredService<ISerializer>();
            var handler = sp.GetRequiredService<IJobHandler<TRequest>>();

            var jsonTypeInfo = AsyncEndpointsJsonSerializationContext.Default.HttpJobPayload;
            var httpPayload = serializer.Deserialize(record.Payload, typeof(HttpJobPayload), jsonTypeInfo) as HttpJobPayload
                ?? throw new InvalidOperationException("Failed to deserialize job payload");

            TRequest request;
            if (bodyTransform is not null)
            {
                request = bodyTransform(httpPayload);
            }
            else
            {
                var requestTypeInfo = (JsonTypeInfo<TRequest>?)AsyncEndpointsJsonSerializationContext.Default.GetTypeInfo(typeof(TRequest));
                request = requestTypeInfo is not null
                    ? serializer.Deserialize(httpPayload.Body, requestTypeInfo)!
                    : serializer.Deserialize<TRequest>(httpPayload.Body, (JsonSerializerOptions?)null)!;
            }

            await handler.HandleAsync(request, ct);
            return null;
        });
    }
}
```

---

## 5. Migration Path for Existing Users

### 5.1 Using the Legacy Obsolete Types (No Action Needed)
All legacy types remain as `[Obsolete]` — existing code using `RouteBuilderExtensions`, `IAsyncEndpointRequestHandler`, etc. continues to compile with warnings.

### 5.2 Migrating from Legacy to New Pipeline

**Before (old):**
```csharp
app.MapAsyncPost<CreateOrderRequest>("create-order", "/orders");
```

**After (new):**
```csharp
app.MapAsyncEndpoint<CreateOrderRequest>("create-order")
   .MapPost("/orders");
```

The handler changes from:
```csharp
class CreateOrderHandler : IAsyncEndpointRequestHandler<CreateOrderRequest, OrderResponse>
{
    public async Task<MethodResult<OrderResponse>> HandleAsync(AsyncContext<CreateOrderRequest> context, CancellationToken token)
    {
        var request = context.Request;
        // Access to headers: context.Headers
        // Access to route params: context.RouteParams
        // Access to query params: context.QueryParams
    }
}
```

To:
```csharp
class CreateOrderHandler : IJobHandler<CreateOrderRequest>
{
    public async Task HandleAsync(CreateOrderRequest request, CancellationToken ct)
    {
        // Worker-side: request is the deserialized body
        // HTTP context available via HttpJobPayload in record.Payload
    }
}
```

---

## 6. Implementation Order

| Phase | What | Dependencies |
|-------|------|-------------|
| **1** | Create `HttpJobPayload` and response DTO models | None |
| **2** | Register new types in `AsyncEndpointsJsonSerializationContext` | Phase 1 |
| **3** | Add `jobName` overload to `IJobSubmitter` + `JobSubmitter` | None |
| **4** | Revive `AsyncEndpointsResponseConfigurations` with new delegates | Phase 1 |
| **5** | Update `ResponseDefaults` for new pipeline | Phase 4 |
| **6** | Update `JobEndpoints.PostJob` — use `ISerializer`, capture HTTP context, use `Results.Problem` | Phase 2, 3 |
| **7** | Update `JobStatusEndpoint` and `JobResultEndpoint` — AOT-safe DTOs, `Results.Problem` | Phase 1, 2 |
| **8** | Create `AsyncEndpointRouteBuilderExtensions` with per-endpoint registration | Phase 3, 4 |
| **9** | Update `EndpointRouteBuilderExtensions.MapAsyncEndpointsEndpoints` to accept response config | Phase 4 |
| **10** | Fix `JsonBodyParserService` for AOT-safe deserialization | Phase 2 |
| **11** | Add handler registration helper extensions | Phase 2 |
| **12** | Update DI registration in `ServiceCollectionExtensions` | Phase 4 |
| **13** | Write/update tests for all new components | All phases |
| **14** | Verify AOT compatibility with `dotnet publish -aot` | All phases |

---

## 7. Backward Compatibility

All changes are additive:
- `IJobSubmitter` gains a new overload — existing implementations continue to work
- `AsyncEndpointsResponseConfigurations` gets un-obsoleted — old references continue to compile
- Legacy `RouteBuilderExtensions`, `AsyncEndpointRequestDelegate`, etc. remain `[Obsolete]` with no behavior change
- The existing `JobEndpoints.PostJob` endpoint continues to exist as a simple/default submission endpoint
- New `AsyncEndpointRouteBuilderExtensions` provides per-endpoint registration without breaking existing code

## 8. Open Questions

1. Should `IJobSubmitter.SubmitAsync(string jobName, ...)` be the primary method and the original become a convenience overload? **Recommendation:** Yes — makes explicit naming the default.

2. Should `AsyncEndpointsResponseConfigurations` be registered as Singleton or Scoped? **Recommendation:** Singleton — the delegates are pure factories with no mutable state.

3. Should we remove `MapAsyncEndpointsEndpoints()` or keep it as a convenience? **Recommendation:** Keep it as a simple default for quick-start scenarios. The new per-endpoint registration is opt-in.
