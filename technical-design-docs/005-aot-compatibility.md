# AsyncEndpoints — AOT Compatibility Design

## Why the Original Design Breaks AOT

Native AOT compilation trims all code that is not statically reachable. The `JobDispatcher` in the core implementation uses three patterns that are incompatible with this:

| Pattern                                          | Location        | Why it breaks                                               |
| ------------------------------------------------ | --------------- | ----------------------------------------------------------- |
| `Type.GetType(string)`                           | `JobDispatcher` | Requires metadata for all types — trimmer cannot know which |
| `typeof(IJobHandler<>).MakeGenericType(jobType)` | `JobDispatcher` | Generic instantiation at runtime is not supported under AOT |
| `(dynamic)handler`                               | `JobDispatcher` | Compiles to reflection emit internally                      |
| `JsonSerializer.Serialize<T>` with no context    | `JobSerializer` | Uses runtime reflection to walk object graph                |
| `Assembly.GetTypes()` in `ScanHandlersFrom`      | DI registration | Assembly scanning is trimming-unsafe                        |

This document replaces all of these with AOT-safe equivalents. No `[RequiresUnreferencedCode]` or `[RequiresDynamicCode]` annotations appear anywhere in the library.

---

## 1. The Core Idea: Close Over Types at Startup, Not at Dispatch

The fundamental shift is this: **move all type knowledge from dispatch time to registration time**.

At dispatch time the store hands you a `JobRecord` with `JobType = "SendEmailJob"` and `PayloadJson = "..."`. In the reflection model you used that string to look up the CLR type at runtime. In the AOT model, startup code has already registered a compiled delegate for `"SendEmailJob"` that knows exactly how to deserialize and handle it — the string is just a dictionary key.

```
Registration time (startup):          Dispatch time (runtime):
  "SendEmailJob" ──► IJobExecutor        jobType string
  (compiled, closed over TJob)     ──►  dictionary lookup
                                   ──►  executor.ExecuteAsync(record)
                                         (no reflection)
```

---

## 2. IJobExecutor — The Non-Generic Dispatch Interface

Replace the generic `IJobHandler<T>` dispatch path with a non-generic executor that the dispatcher can call without knowing `T`.

```csharp
// Abstractions project
public interface IJobExecutor
{
    /// <summary>
    /// Deserializes the payload from the job record and invokes the handler.
    /// All type knowledge is baked in at registration time.
    /// </summary>
    Task ExecuteAsync(JobRecord record, CancellationToken ct);
}
```

The generic implementation lives in Core and is never exposed to the dispatcher:

```csharp
// Core/Execution/JobExecutor.cs  (internal)
internal sealed class JobExecutor<TJob> : IJobExecutor
    where TJob : class
{
    private readonly IJobHandler<TJob>            _handler;
    private readonly Func<string, TJob>           _deserialize;

    public JobExecutor(IJobHandler<TJob> handler, Func<string, TJob> deserialize)
    {
        _handler     = handler;
        _deserialize = deserialize;
    }

    public async Task ExecuteAsync(JobRecord record, CancellationToken ct)
    {
        var job = _deserialize(record.PayloadJson);
        await _handler.HandleAsync(job, ct);
    }
}
```

`JobExecutor<TJob>` is fully AOT-safe:

- `TJob` is statically known at construction time
- `_deserialize` is a compiled delegate — no `JsonSerializer` reflection at call time
- `_handler` is injected from DI — no `GetRequiredService` with a runtime `Type`

---

## 3. JobTypeRegistry — String to Executor Map

```csharp
// Core/Execution/JobTypeRegistry.cs
public sealed class JobTypeRegistry
{
    private readonly Dictionary<string, Func<IServiceProvider, IJobExecutor>> _factories = new();

    /// <summary>
    /// Called once per job type at startup.
    /// </summary>
    public void Register<TJob>(Func<string, TJob> deserialize)
        where TJob : class
    {
        var key = typeof(TJob).Name; // stored in JobRecord.JobType at enqueue

        _factories[key] = sp =>
        {
            var handler = sp.GetRequiredService<IJobHandler<TJob>>();
            return new JobExecutor<TJob>(handler, deserialize);
        };
    }

    /// <summary>
    /// Resolves the executor for a job type name.
    /// Throws UnknownJobTypeException (no reflection fallback).
    /// </summary>
    public IJobExecutor Resolve(string jobType, IServiceProvider sp)
    {
        if (!_factories.TryGetValue(jobType, out var factory))
            throw new UnknownJobTypeException(jobType);

        return factory(sp);
    }
}
```

`GetRequiredService<IJobHandler<TJob>>()` is called with a **statically known** generic parameter — this is a normal generic method call, not a runtime `MakeGenericType`. The trimmer can see it.

---

## 4. AOT-Safe Serialization via JsonSerializerContext

`System.Text.Json` source generation produces a `JsonSerializerContext` subclass at compile time. The library defines a partial context; each application extends it with its own job types.

### 4a. Library-side context (ships in the package)

```csharp
// Core/Serialization/AsyncEndpointsJsonContext.cs
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(JobStatusResponse))]
[JsonSerializable(typeof(JobSubmitResult))]
public partial class AsyncEndpointsJsonContext : JsonSerializerContext { }
```

### 4b. Per-job-type serializer registration

Each job type needs its own serialize/deserialize pair. These are registered as delegates so no `JsonSerializer.Serialize<T>(obj, options)` reflection path is invoked at dispatch time.

The registration helper generates these delegates from the source-generated context:

```csharp
// Core/DependencyInjection/ServiceCollectionExtensions.cs
public static IAsyncEndpointsBuilder AddJobType<TJob>(
    this IAsyncEndpointsBuilder builder,
    JsonTypeInfo<TJob> jsonTypeInfo)  // <── caller passes the source-gen type info
    where TJob : class
{
    builder.Registry.Register<TJob>(
        deserialize: json => JsonSerializer.Deserialize(json, jsonTypeInfo)
                             ?? throw new JobDeserializationException(typeof(TJob).Name, json)
    );

    // Also register the serializer for use at enqueue time
    builder.SerializerRegistry.Register<TJob>(
        serialize: job => JsonSerializer.Serialize(job, jsonTypeInfo)
    );

    return builder;
}
```

`JsonTypeInfo<TJob>` is produced by the source generator — it is a concrete, statically typed object. Passing it here means zero reflection at both serialize and deserialize time.

### 4c. How the caller wires it up

The user defines their own `JsonSerializerContext` for their job types:

```csharp
// Application code
[JsonSerializable(typeof(SendEmailJob))]
[JsonSerializable(typeof(GenerateReportJob))]
public partial class MyJobsJsonContext : JsonSerializerContext { }
```

Then registers job types with their type info:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.UsePostgres(connectionString);
})
.AddJobType<SendEmailJob>(MyJobsJsonContext.Default.SendEmailJob)
.AddJobType<GenerateReportJob>(MyJobsJsonContext.Default.GenerateReportJob);
```

`MyJobsJsonContext.Default.SendEmailJob` is a `JsonTypeInfo<SendEmailJob>` — a plain object reference, no reflection involved.

---

## 5. AOT-Safe Dispatcher

With the registry in place, the dispatcher becomes trivial and reflection-free:

```csharp
// Core/Execution/JobDispatcher.cs
internal sealed class JobDispatcher
{
    private readonly JobTypeRegistry _registry;
    private readonly IServiceProvider _sp;

    public JobDispatcher(JobTypeRegistry registry, IServiceProvider sp)
    {
        _registry = registry;
        _sp       = sp;
    }

    public Task DispatchAsync(JobRecord record, CancellationToken ct)
    {
        // Only operation: dictionary lookup by job type name string
        var executor = _registry.Resolve(record.JobType, _sp);
        return executor.ExecuteAsync(record, ct);
    }
}
```

No `Type.GetType`, no `MakeGenericType`, no `dynamic`. The trimmer can statically analyse all code paths.

---

## 6. Replacing ScanHandlersFrom — Source Generator

`ScanHandlersFrom(assembly)` must be removed entirely. It is replaced by a **Roslyn source generator** that emits the `AddJobType` registration calls automatically.

### What the generator does

At compile time it scans the user's assembly for:

- All classes implementing `IJobHandler<TJob>`
- All types `TJob` referenced by those handlers

It then emits a static extension method `AddDiscoveredJobHandlers` in the user's namespace:

```csharp
// Generated: AsyncEndpoints.Generated.cs  (emitted into user's project)
public static partial class AsyncEndpointsGeneratedExtensions
{
    public static IAsyncEndpointsBuilder AddDiscoveredJobHandlers(
        this IAsyncEndpointsBuilder builder)
    {
        builder.Services.AddScoped<IJobHandler<SendEmailJob>, SendEmailHandler>();
        builder.Services.AddScoped<IJobHandler<GenerateReportJob>, GenerateReportHandler>();

        builder
            .AddJobType<SendEmailJob>(MyJobsJsonContext.Default.SendEmailJob)
            .AddJobType<GenerateReportJob>(MyJobsJsonContext.Default.GenerateReportJob);

        return builder;
    }
}
```

The user's startup becomes:

```csharp
builder.Services
    .AddAsyncEndpoints(options => options.UsePostgres(connectionString))
    .AddDiscoveredJobHandlers();  // source-generated, zero reflection
```

### What the generator needs from the user

The generator needs to know which `JsonSerializerContext` to use for the `TypeInfo` references. The user annotates their context class:

```csharp
[AsyncEndpointsJobContext]         // marker attribute — tells the generator which context to use
[JsonSerializable(typeof(SendEmailJob))]
[JsonSerializable(typeof(GenerateReportJob))]
public partial class MyJobsJsonContext : JsonSerializerContext { }
```

The generator then emits `MyJobsJsonContext.Default.SendEmailJob` in the generated registrations, which is a compile-time reference with no reflection.

### Fallback: manual registration

Users who prefer not to use the source generator (or who have unusual project layouts) can register manually:

```csharp
builder.Services.AddScoped<IJobHandler<SendEmailJob>, SendEmailHandler>();

builder.Services
    .AddAsyncEndpoints(options => options.UsePostgres(connectionString))
    .AddJobType<SendEmailJob>(MyJobsJsonContext.Default.SendEmailJob);
```

Both paths are equivalent — the source generator just saves the boilerplate.

---

## 7. AOT-Safe Enqueue Path

The enqueue side also serializes the payload. `JobSubmitter` is updated to use the same registered delegate, not a raw `JsonSerializer.Serialize` call:

```csharp
// Core/Submission/JobSubmitter.cs
internal sealed class JobSubmitter : IJobSubmitter
{
    private readonly IJobStore            _store;
    private readonly JobSerializerRegistry _serializers;  // parallel to JobTypeRegistry
    private readonly IJobNotifier?        _notifier;
    private readonly AsyncEndpointsOptions _options;

    public async Task<JobSubmitResult> SubmitAsync<TJob>(
        TJob job,
        string? channel = null,
        int? priority = null,
        object? partitionBy = null,
        CancellationToken ct = default)
        where TJob : class
    {
        // Serialize via registered delegate — no reflection
        var payloadJson = _serializers.Serialize(job);

        var descriptor = new JobDescriptor
        {
            JobType     = typeof(TJob).Name,     // typeof() is AOT-safe
            PayloadJson = payloadJson,
            PayloadType = typeof(TJob).Name,     // store simple name; resolution is via registry
            Channel     = channel   ?? _options.DefaultChannel,
            Priority    = priority  ?? _options.DefaultPriority,
            Partition   = partitionBy != null
                            ? PartitionResolver.Resolve(partitionBy, _options.PartitionCount)
                            : null,
            MaxRetries  = _options.MaxRetries,
        };

        var jobId = await _store.EnqueueAsync(descriptor, ct);
        if (_notifier != null)
            await _notifier.NotifyJobAvailableAsync(descriptor.Channel, ct);

        return new JobSubmitResult(jobId, $"/jobs/{jobId}");
    }
}
```

Note that `PayloadType` now stores just `typeof(TJob).Name` (e.g. `"SendEmailJob"`) rather than the assembly-qualified name. The registry is keyed by this name, so `Type.GetType()` is never needed to resolve it. This also makes the stored value shorter and less brittle across assembly renames.

---

## 8. JobSerializerRegistry

Mirrors `JobTypeRegistry` for the enqueue direction:

```csharp
// Core/Serialization/JobSerializerRegistry.cs
internal sealed class JobSerializerRegistry
{
    private readonly Dictionary<string, Func<object, string>> _serializers = new();

    public void Register<TJob>(Func<TJob, string> serialize) where TJob : class
    {
        _serializers[typeof(TJob).Name] = obj => serialize((TJob)obj);
    }

    public string Serialize<TJob>(TJob job) where TJob : class
    {
        if (!_serializers.TryGetValue(typeof(TJob).Name, out var fn))
            throw new UnknownJobTypeException(typeof(TJob).Name);

        return fn(job);
    }
}
```

The `(TJob)obj` cast in the delegate is a direct cast between statically known types — it compiles to a `castclass` IL instruction, which is AOT-safe.

---

## 9. Trimmer Root Descriptors

Even with source generation, some library internals need explicit trimmer roots to survive the linker. Ship a `ILLink.Descriptors.xml` in each package:

```xml
<!-- AsyncEndpoints.Core/ILLink.Descriptors.xml -->
<linker>
  <assembly fullname="AsyncEndpoints.Core">
    <type fullname="AsyncEndpoints.Core.Execution.JobExecutor`1" preserve="all"/>
    <type fullname="AsyncEndpoints.Core.Execution.JobTypeRegistry" preserve="all"/>
    <type fullname="AsyncEndpoints.Core.Serialization.JobSerializerRegistry" preserve="all"/>
  </assembly>
</linker>
```

And annotate the package's `.csproj` to include it:

```xml
<ItemGroup>
  <EmbeddedResource
    Include="ILLink.Descriptors.xml"
    LogicalName="ILLink.Descriptors.xml" />
</ItemGroup>
```

---

## 10. Marking the Public API as AOT-Compatible

The library's public entry points should carry the positive annotation so AOT-enabled projects get no warnings:

```csharp
[RequiresUnreferencedCode("...")]   // ← we do NOT add this
[RequiresDynamicCode("...")]        // ← we do NOT add this
```

Instead, ship a `RuntimeCompatibility.cs` confirming no dynamic code is used:

```csharp
// Ensures NativeAOT analysis does not warn on library consumers
[assembly: System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
```

And in the `.csproj` of each package, opt into AOT analysis at build time so CI catches regressions:

```xml
<PropertyGroup>
  <IsAotCompatible>true</IsAotCompatible>   <!-- enables AOT trim/rdc analysis warnings -->
  <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
  <EnableSingleFileAnalyzer>true</EnableSingleFileAnalyzer>
</PropertyGroup>
```

With `IsAotCompatible=true`, the compiler runs the same static analysis that the AOT publisher would run — any reflection path that sneaks in will produce a build warning immediately, not a runtime crash in production.

---

## 11. Updated Folder Structure

The AOT changes add a small number of new files. Changes relative to 002 are marked with `[NEW]` or `[CHANGED]`.

```
AsyncEndpoints.Core/
│
├── Execution/
│   ├── JobDispatcher.cs         [CHANGED — reflection removed]
│   ├── JobExecutor.cs           [NEW — IJobExecutor<T> implementation]
│   ├── JobTypeRegistry.cs       [NEW — string → executor map]
│   └── JobExecutionPipeline.cs
│
├── Serialization/
│   ├── JobSerializerRegistry.cs [NEW — string → serialize delegate map]
│   └── AsyncEndpointsJsonContext.cs [NEW — library-side JsonSerializerContext]
│
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs [CHANGED — AddJobType<T> added]
│
└── ILLink.Descriptors.xml       [NEW — trimmer roots]

AsyncEndpoints.SourceGenerator/  [NEW project]
│
├── JobHandlerDiscoveryGenerator.cs
├── JobContextLocator.cs
└── AsyncEndpointsGeneratedExtensions.template
```

The source generator is a separate project with `<OutputItemType>Analyzer</OutputItemType>` so it ships as a Roslyn analyzer alongside the Core package and runs transparently during the user's build.

---

## Summary of Changes from 004

| Area                    | Before (004)                          | After (this doc)                                             |
| ----------------------- | ------------------------------------- | ------------------------------------------------------------ |
| Handler dispatch        | `MakeGenericType` + `dynamic`         | `JobTypeRegistry` dictionary + `IJobExecutor`                |
| Payload deserialization | `JsonSerializer` reflection           | `JsonTypeInfo<T>` delegate registered at startup             |
| Payload serialization   | `JsonSerializer` reflection           | `JobSerializerRegistry` delegate                             |
| Type resolution         | `Type.GetType(assemblyQualifiedName)` | Registry key = simple type name, no CLR lookup               |
| Handler discovery       | `Assembly.GetTypes()` scan            | Roslyn source generator emits registrations                  |
| AOT build validation    | None                                  | `<IsAotCompatible>true</IsAotCompatible>` in every `.csproj` |

The public API surface is unchanged — `SubmitAsync<TJob>`, `IJobHandler<TJob>`, and the options builder look identical to the consumer. The only user-visible addition is the `[AsyncEndpointsJobContext]` attribute on their `JsonSerializerContext`, which the source generator uses to wire everything together.
