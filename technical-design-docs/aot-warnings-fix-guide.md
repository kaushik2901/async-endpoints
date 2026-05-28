# AOT Warnings Fix Guide

> Generated from `dotnet build -f net10.0` — all warnings filtered to non-obsolete source code only.

## Summary

| Warning | Source Project | Count | In Obsolete Code? | Status |
|---------|---------------|-------|-------------------|--------|
| IL2026 | `AsyncEndpoints.Provider.Redis` | 2 | No | Needs fix |
| IL3050 | `AsyncEndpoints.Provider.Redis` | 2 | No | Needs fix |
| IL2026 | `AsyncEndpoints.AspNetCore` (serialization fallback) | 2 | No | Needs fix |
| IL3050 | `AsyncEndpoints.AspNetCore` (serialization fallback) | 2 | No | Needs fix |
| IL2026 | `AsyncEndpoints.AspNetCore` (MapGet/MapPost/MapPut/MapPatch/MapDelete) | 3 | No | Needs fix |
| IL3050 | `AsyncEndpoints.AspNetCore` (MapGet/MapPost/MapPut/MapPatch/MapDelete) | 3 | No | Needs fix |
| IL2026 | `AsyncEndpoints.AspNetCore` (RouteBuilderExtensions) | 9 | **Yes** | Ignore |
| IL3050 | `AsyncEndpoints.AspNetCore` (RouteBuilderExtensions) | 9 | **Yes** | Ignore |
| IL2026 | `AsyncEndpoints.AspNetCore` (JobResultResponse) | 1 | **Yes** | Ignore |
| IL3050 | `AsyncEndpoints.AspNetCore` (JobResultResponse) | 1 | **Yes** | Ignore |
| IL2026 | `AsyncEndpoints.AspNetCore` (AsyncEndpointRequestDelegate) | 1 | **Yes** | Ignore |
| IL3050 | `AsyncEndpoints.AspNetCore` (AsyncEndpointRequestDelegate) | 1 | **Yes** | Ignore |

**Non-obsolete AOT warnings to fix: 14 total (7 IL2026 + 7 IL3050)**

---

## Warnings Ignored (Obsolete Code)

The following types are marked `[Obsolete]` — warnings within them are **safe to suppress**:

- `RouteBuilderExtensions` → all `MapPost`/`MapPut`/`MapPatch`/`MapDelete`/`MapGet` calls
- `JobResultResponse` → `ISerializer.Serialize<T>` call
- `AsyncEndpointRequestDelegate` → `ISerializer.Serialize<T>` call

---

## Fix: `JobHashConverter.cs` (IL2026, IL3050)

**File:** `src/AsyncEndpoints.Provider.Redis/Services/JobHashConverter.cs:30,91`

**Warnings:**
```
IL2026: JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions) — RequiresUnreferencedCode
IL3050: JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions) — RequiresDynamicCode
IL2026: JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions) — RequiresUnreferencedCode  
IL3050: JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions) — RequiresDynamicCode
```

**Root cause:** Using the reflection-based `JsonSerializer.Serialize<T>(T, JsonSerializerOptions?)` and `JsonSerializer.Deserialize<T>(string, JsonSerializerOptions?)` overloads for `Dictionary<string, string>` metadata.

**Option A: Create a source-generated JSON context in the Redis project** (recommended)

Add a new file `src/AsyncEndpoints.Provider.Redis/Infrastructure/RedisJsonSerializationContext.cs`:

```csharp
using System.Text.Json.Serialization;

namespace AsyncEndpoints.Provider.Redis;

[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class RedisJsonSerializationContext : JsonSerializerContext
{
}
```

Then update `JobHashConverter.cs`:

```csharp
// Line 30 — Serialize
new HashEntry("Metadata", record.Metadata is not null
    ? JsonSerializer.Serialize(record.Metadata, RedisJsonSerializationContext.Default.DictionaryStringString)
    : "")

// Lines 88-93 — Deserialize
private static Dictionary<string, string>? DeserializeMetadata(string? value)
{
    if (string.IsNullOrEmpty(value)) return null;
    try { return JsonSerializer.Deserialize(value, RedisJsonSerializationContext.Default.DictionaryStringString); }
    catch { return null; }
}
```

**Option B: Add `Dictionary<string, string>` to `AsyncEndpointsJsonSerializationContext`** (requires adding Core project reference to Redis, not currently referenced)

---

## Fix: `JsonBodyParserService.cs` (IL2026, IL3050)

**File:** `src/AsyncEndpoints.AspNetCore/Serialization/JsonBodyParserService.cs:54`

**Warning:**
```
IL2026: ISerializer.DeserializeAsync<T>(Stream, JsonSerializerOptions, CancellationToken) — RequiresUnreferencedCode
IL3050: ISerializer.DeserializeAsync<T>(Stream, JsonSerializerOptions, CancellationToken) — RequiresDynamicCode
```

**Root cause:** Fallback branch at line 54 is called when neither `AsyncEndpointsAspNetCoreJsonSerializationContext` nor `AsyncEndpointsJsonSerializationContext` has a `JsonTypeInfo<T>` for the requested type `T`.

**Option A: Validate at registration time** (recommended for library code)

Since `JsonBodyParserService` is a library-internal service that only processes types the user has registered, the safest fix is to propagate the trimming/AOT requirements to the public API surface. Add `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]` to `IJsonBodyParserService.ParseAsync<T>()` and the `JsonBodyParserService.ParseAsync<T>()` implementation. This pushes the concern to the consumer who chooses which types to use.

```csharp
// In IJsonBodyParserService.cs and JsonBodyParserService.cs:
[RequiresUnreferencedCode("T must have a JsonTypeInfo registered in a JsonSerializerContext for AOT compatibility.")]
[RequiresDynamicCode("T must have a JsonTypeInfo registered in a JsonSerializerContext for AOT compatibility.")]
public async Task<MethodResult<T?>> ParseAsync<T>(HttpContext httpContext, CancellationToken cancellationToken = default)
```

**Option B: Ensure all expected types are registered in `AsyncEndpointsAspNetCoreJsonSerializationContext` or `AsyncEndpointsJsonSerializationContext`** — then the fallback is never reached and the warning disappears. However, since `T` is user-defined, this is only viable for known types.

---

## Fix: `EndpointRouteBuilderExtensions.cs` (IL2026, IL3050)

**File:** `src/AsyncEndpoints.AspNetCore/Extensions/EndpointRouteBuilderExtensions.cs:17,18,19`

**Warning:**
```
IL2026: MapPost/IEndpointRouteBuilder, String, Delegate) — RequiresUnreferencedCode
IL3050: MapPost/IEndpointRouteBuilder, String, Delegate) — RequiresDynamicCode  
IL2026: MapGet(IEndpointRouteBuilder, String, Delegate) — RequiresUnreferencedCode  
IL3050: MapGet(IEndpointRouteBuilder, String, Delegate) — RequiresDynamicCode
```

**Root cause:** `MapPost`/`MapGet` overloads that accept `Delegate` perform reflection on the delegate parameters. ASP.NET Core has annotated these with `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`.

**Fix:** Propagate `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]` attributes to the `MapAsyncEndpointsEndpoints` methods:

```csharp
[RequiresUnreferencedCode("MapAsyncEndpointsEndpoints uses MapPost/MapGet with Delegate arguments which perform reflection.")]
[RequiresDynamicCode("MapAsyncEndpointsEndpoints uses MapPost/MapGet with Delegate arguments which require dynamic code.")]
public static IEndpointRouteBuilder MapAsyncEndpointsEndpoints(
    this IEndpointRouteBuilder routes,
    string? prefix = null)
```

> **Note:** This is an inherent limitation of the `Delegate`-based `Map*` APIs. For full AOT compatibility, the endpoints would need to use the typed `MapPost<T>(...)` or `MapGet<T>(...)` overloads (if available in .NET 10) or be restructured to use `RequestDelegate` directly instead of method groups.

---

## Fix: `AsyncEndpointRouteBuilderExtensions.cs` (IL2026, IL3050)

**File:** `src/AsyncEndpoints.AspNetCore/Extensions/AsyncEndpointRouteBuilderExtensions.cs:177`

**Warning:**
```
IL2026: ISerializer.Deserialize<T>(String, JsonSerializerOptions) — RequiresUnreferencedCode
IL3050: ISerializer.Deserialize<T>(String, JsonSerializerOptions) — RequiresDynamicCode
```

**Root cause:** Same as `JsonBodyParserService.cs` — the fallback branch at line 177 is used when neither `AsyncEndpointsAspNetCoreJsonSerializationContext` nor `AsyncEndpointsJsonSerializationContext` has a `JsonTypeInfo<TRequest>` for the request type.

**Fix:** Propagate `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]` to the private `DeserializeBody<TRequest>` method and the public `MapAsyncPost<TRequest>`, `MapAsyncPut<TRequest>`, `MapAsyncPatch<TRequest>`, `MapAsyncDelete<TRequest>` methods:

```csharp
[RequiresUnreferencedCode("TRequest must have a JsonTypeInfo registered in a JsonSerializerContext for AOT compatibility.")]
[RequiresDynamicCode("TRequest must have a JsonTypeInfo registered in a JsonSerializerContext for AOT compatibility.")]
private static TRequest? DeserializeBody<TRequest>(ISerializer serializer, string bodyText)
```

And on the public extension methods:

```csharp
[RequiresUnreferencedCode("Uses reflection-based deserialization fallback for TRequest without registered JsonTypeInfo.")]
[RequiresDynamicCode("Uses reflection-based deserialization fallback for TRequest without registered JsonTypeInfo.")]
public static IEndpointConventionBuilder MapAsyncPost<TRequest>(...)
```

---

## Complete Warning Inventory

### Non-obsolete source (needs fixing)

| File | Lines | Warning | Fix Strategy |
|------|-------|---------|-------------|
| `src/AsyncEndpoints.Provider.Redis/Services/JobHashConverter.cs` | 30, 91 | IL2026, IL3050 | Use source-generated `JsonTypeInfo<Dictionary<string, string>>` |
| `src/AsyncEndpoints.AspNetCore/Serialization/JsonBodyParserService.cs` | 54 | IL2026, IL3050 | Propagate `[RequiresUnreferencedCode]` + `[RequiresDynamicCode]` |
| `src/AsyncEndpoints.AspNetCore/Extensions/EndpointRouteBuilderExtensions.cs` | 17, 18, 19 | IL2026, IL3050 | Propagate `[RequiresUnreferencedCode]` + `[RequiresDynamicCode]` |
| `src/AsyncEndpoints.AspNetCore/Extensions/AsyncEndpointRouteBuilderExtensions.cs` | 177 | IL2026, IL3050 | Propagate `[RequiresUnreferencedCode]` + `[RequiresDynamicCode]` |

### Obsolete source (can suppress/ignore)

| File | Lines | Warning | Obsolete Type |
|------|-------|---------|--------------|
| `src/AsyncEndpoints.AspNetCore/Extensions/RouteBuilderExtensions.cs` | 34, 51, 71, 88, 108, 125, 145, 162, 175 | IL2026, IL3050 | `RouteBuilderExtensions` class |
| `src/AsyncEndpoints.AspNetCore/Endpoints/JobResultResponse.cs` | 31 | IL2026, IL3050 | `JobResultResponse` class |
| `src/AsyncEndpoints.AspNetCore/Handlers/AsyncEndpointRequestDelegate.cs` | 38 | IL2026, IL3050 | `AsyncEndpointRequestDelegate` class |

---

## Recommended Suppression for Obsolete Code

Add `#pragma warning disable` in each obsolete file or in `Directory.Build.props`:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);IL2026;IL3050</NoWarn>
</PropertyGroup>
```

Or per-file suppressions (already part of `AsyncEndpointsJsonSerializationContext.cs`):

```csharp
#pragma warning disable IL2026, IL3050 // AOT warnings suppressed — obsolete code
```
