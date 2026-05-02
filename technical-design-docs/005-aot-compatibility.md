# AsyncEndpoints — AOT Compatibility

This document describes how AsyncEndpoints is designed to be AOT-compatible from day one. The runtime contains no reflection-based type discovery, no `dynamic` invocation, and no runtime generic construction. All serialization and dispatch paths are statically analyzable and trimming-safe.

---

## Core Principles

- No runtime type lookup (`Type.GetType`, runtime `MakeGenericType`) — dispatch uses a registry keyed by simple type names.
- No assembly scanning — handler registration is explicit or source-generated at build time.
- Source-generated JSON — serialization/deserialization use `JsonSerializerContext` and `JsonTypeInfo<T>`.
- Stable contract keys — `JobRecord.JobType`/`PayloadType` store the simple type name, not assembly-qualified names.
- Build-time validation — packages enable trim/AOT analyzers and ship minimal trimmer roots.

---

## 1) Dispatch Model

Dispatch is performed through a non-generic `IJobExecutor` resolved from `JobTypeRegistry` using the `JobType` string on `JobRecord`.

```csharp
public interface IJobExecutor { Task ExecuteAsync(JobRecord record, CancellationToken ct); }

internal sealed class JobExecutor<TJob> : IJobExecutor where TJob : class
{
    private readonly IJobHandler<TJob> _handler;
    private readonly Func<string, TJob> _deserialize;
    public JobExecutor(IJobHandler<TJob> handler, Func<string, TJob> deserialize)
    { _handler = handler; _deserialize = deserialize; }
    public Task ExecuteAsync(JobRecord record, CancellationToken ct)
        => _handler.HandleAsync(_deserialize(record.PayloadJson), ct);
}

public sealed class JobTypeRegistry
{
    private readonly Dictionary<string, Func<IServiceProvider, IJobExecutor>> _factories = new();
    public void Register<TJob>(Func<string, TJob> deserialize) where TJob : class
    {
        var key = typeof(TJob).Name;
        _factories[key] = sp => new JobExecutor<TJob>(sp.GetRequiredService<IJobHandler<TJob>>(), deserialize);
    }
    public IJobExecutor Resolve(string key, IServiceProvider sp)
        => _factories.TryGetValue(key, out var f) ? f(sp) : throw new UnknownJobTypeException(key);
}

internal sealed class JobDispatcher
{
    private readonly JobTypeRegistry _registry; private readonly IServiceProvider _sp;
    public JobDispatcher(JobTypeRegistry registry, IServiceProvider sp) { _registry = registry; _sp = sp; }
    public Task DispatchAsync(JobRecord record, CancellationToken ct)
        => _registry.Resolve(record.JobType, _sp).ExecuteAsync(record, ct);
}
```

---

## 2) Serialization Model

Serialization uses source-generated `JsonSerializerContext` and per-type `JsonTypeInfo<T>` to avoid runtime reflection.

```csharp
// Library context (ships in package)
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(JobStatusResponse))]
[JsonSerializable(typeof(JobSubmitResult))]
public partial class AsyncEndpointsJsonContext : JsonSerializerContext { }

// Serializer registry
internal sealed class JobSerializerRegistry
{
    private readonly Dictionary<string, Func<object, string>> _ser = new();
    private readonly Dictionary<string, Func<string, object>> _de  = new();
    public void Register<TJob>(JsonTypeInfo<TJob> info) where TJob : class
    { var k = typeof(TJob).Name; _ser[k] = o => JsonSerializer.Serialize((TJob)o, info);
      _de[k] = j => JsonSerializer.Deserialize(j, info)!; }
    public string Serialize<TJob>(TJob job) where TJob : class => _ser[typeof(TJob).Name](job);
    public object Deserialize(string key, string json) => _de[key](json);
}
```

Applications extend the context and register job types:

```csharp
[JsonSerializable(typeof(SendEmailJob))]
public partial class MyJobsJsonContext : JsonSerializerContext { }

builder.Services
    .AddAsyncEndpoints(o => o.UsePostgres(connectionString))
    .AddJobType<SendEmailJob>(MyJobsJsonContext.Default.SendEmailJob);
```

---

## 3) Registration Options

- Manual: add handlers in DI and call `.AddJobType<T>(JsonTypeInfo<T>)` per job.
- Source generator: a Roslyn analyzer discovers `IJobHandler<T>` implementations at compile time and emits an `AddDiscoveredJobHandlers()` extension that registers both handlers and corresponding `AddJobType<T>(...)` calls. No runtime scanning is used.

The generator uses a marker attribute on the application's `JsonSerializerContext` to locate type infos.

```csharp
[AsyncEndpointsJobContext]
[JsonSerializable(typeof(SendEmailJob))]
public partial class MyJobsJsonContext : JsonSerializerContext { }
```

---

## 4) Enqueue Path

`JobSubmitter` serializes via `JobSerializerRegistry` and stores `JobType`/`PayloadType` as the simple name (e.g., `"SendEmailJob"`).

```csharp
var descriptor = new JobDescriptor
{
    JobType     = typeof(TJob).Name,
    PayloadJson = _serializers.Serialize(job),
    PayloadType = typeof(TJob).Name,
    // channel, priority, partition, retries...
};
```

---

## 5) Build-Time AOT Validation

Every package opts into AOT analysis to catch regressions early:

```xml
<PropertyGroup>
  <IsAotCompatible>true</IsAotCompatible>
  <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
  <EnableSingleFileAnalyzer>true</EnableSingleFileAnalyzer>
</PropertyGroup>
```

If specific internals must be preserved, include a minimal `ILLink.Descriptors.xml`:

```xml
<linker>
  <assembly fullname="AsyncEndpoints.Core">
    <type fullname="AsyncEndpoints.Core.Execution.JobExecutor`1" preserve="all"/>
    <type fullname="AsyncEndpoints.Core.Execution.JobTypeRegistry" preserve="all"/>
    <type fullname="AsyncEndpoints.Core.Serialization.JobSerializerRegistry" preserve="all"/>
  </assembly>
  </linker>
```

---

## 6) Folder Structure Additions

Relative to 002, the AOT-first design includes:

```
AsyncEndpoints.Core/
├── Execution/
│   ├── JobDispatcher.cs
│   ├── JobExecutor.cs
│   └── JobTypeRegistry.cs
├── Serialization/
│   ├── JobSerializerRegistry.cs
│   └── AsyncEndpointsJsonContext.cs
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs
└── ILLink.Descriptors.xml

AsyncEndpoints.SourceGenerator/
├── JobHandlerDiscoveryGenerator.cs
├── JobContextLocator.cs
└── AsyncEndpointsGeneratedExtensions.template
```

---

## 7) What Consumers See

- Keep using `SubmitAsync<TJob>(...)` and implement `IJobHandler<TJob>`.
- Register jobs via `.AddJobType<T>(JsonTypeInfo<T>)` or call `.AddDiscoveredJobHandlers()` if using the source generator.
- No extra attributes on handlers; only the application's `JsonSerializerContext` needs `[JsonSerializable]` entries for job contracts.

With these guarantees, AsyncEndpoints builds and runs cleanly under Native AOT without suppressions or reflection hints.

