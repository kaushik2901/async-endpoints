# AsyncEndpoints — Core Implementation Reference (AOT-Native)

## Overview

This document covers the concrete implementation details that bridge the high-level design (001) and the folder structure (002) into working code. It is the authoritative reference for the `JobRecord` schema, state machine transition rules, atomic dequeue mechanics, heartbeat/sweeper contracts, retry and dead-letter behaviour, AOT-safe payload serialization/dispatch, and the full `IJobStore` method contracts.

---

## 1. JobRecord — Full Schema

```csharp
public sealed class JobRecord
{
    // Identity
    public Guid   JobId      { get; init; } = Guid.NewGuid();
    public string JobType    { get; init; } = string.Empty; // e.g. "SendEmailJob" (job contract key)
    public string Channel    { get; init; } = "default";
    public int    Priority   { get; init; } = 5;            // lower = more urgent
    public int?   Partition  { get; init; }                 // null when partitioning disabled

    // Payload
    public string PayloadJson { get; init; } = string.Empty; // source-generated System.Text.Json
    public string PayloadType { get; init; } = string.Empty; // same as JobType (simple name); no CLR lookup

    // Status
    public JobStatus Status    { get; set; } = JobStatus.Queued;
    public int       RetryCount { get; set; } = 0;
    public int       MaxRetries { get; init; } = 3;

    // Worker tracking
    public string? WorkerId       { get; set; } // set when Status → Processing
    public DateTimeOffset? StartedAt     { get; set; }
    public DateTimeOffset? LastHeartbeat { get; set; }

    // Timestamps
    public DateTimeOffset CreatedAt   { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    // Results
    public string? ResultJson    { get; set; } // populated on Completed
    public string? ErrorMessage  { get; set; } // populated on Failed / DeadLettered
    public string? ErrorType     { get; set; } // exception type short name

    // Retry scheduling
    public DateTimeOffset? RunAfter { get; set; } // null = run immediately
}
```

### Storage-Agnostic Notes

This document does not prescribe a concrete database schema. Providers define their own storage layout and indexes that satisfy the core invariants (atomic dequeue, guarded transitions, efficient lookups). The `JobRecord` class above is the canonical in-memory shape used by the engine and the `IJobStore` contract.

---

## 2. JobStatus — Enum and Transitions

```csharp
public enum JobStatus : short
{
    Queued       = 0,
    Processing   = 1,
    Completed    = 2,
    Failed       = 3,
    DeadLettered = 4,
    Cancelled    = 5
}
```

### Legal State Transitions

Only the transitions listed here are permitted. Any attempt to write a transition not in this table must throw `InvalidJobStatusTransitionException`.

| From         | To           | Trigger                                      | Who           |
| ------------ | ------------ | -------------------------------------------- | ------------- |
| Queued       | Processing   | `DequeueAsync` — worker claims the job       | Worker        |
| Queued       | Cancelled    | `CancelAsync` — caller requests cancellation | API / Sweeper |
| Processing   | Completed    | Handler returned successfully                | Worker        |
| Processing   | Failed       | Handler threw; retries remain                | Worker        |
| Processing   | Queued       | Sweeper reclaims stale job (heartbeat lapse) | Sweeper       |
| Processing   | Cancelled    | Cancellation was requested while running     | Worker        |
| Failed       | Queued       | Retry scheduler re-queues (sets `RunAfter`)  | Worker        |
| Failed       | DeadLettered | `RetryCount >= MaxRetries`                   | Worker        |
| DeadLettered | Queued       | Manual re-queue via API                      | API           |

```
Queued
  │  ╔═══════════════╗
  ├──► Processing ───► Completed
  │    │    ▲
  │    │    │ (sweeper reclaim)
  │    ▼    │
  │   Failed ───────► Queued (retry, RunAfter set)
  │    │
  │    └────────────► DeadLettered ──► Queued (manual)
  │
  └────────────────► Cancelled
```

### Transition Guard (pseudo-code)

```csharp
internal static void AssertTransitionAllowed(JobStatus from, JobStatus to)
{
    bool allowed = (from, to) switch
    {
        (Queued,       Processing)   => true,
        (Queued,       Cancelled)    => true,
        (Processing,   Completed)    => true,
        (Processing,   Failed)       => true,
        (Processing,   Queued)       => true,  // sweeper only
        (Processing,   Cancelled)    => true,
        (Failed,       Queued)       => true,
        (Failed,       DeadLettered) => true,
        (DeadLettered, Queued)       => true,
        _ => false
    };

    if (!allowed)
        throw new InvalidJobStatusTransitionException(from, to);
}
```

---

## 3. IJobStore — Full Contract

```csharp
public interface IJobStore
{
    /// <summary>
    /// Persist a new job. Returns the assigned JobId.
    /// The job is created with Status=Queued and RetryCount=0.
    /// </summary>
    Task<Guid> EnqueueAsync(JobDescriptor descriptor, CancellationToken ct = default);

    /// <summary>
    /// Atomically claim one job from the given channel (and optionally partition list).
    /// Sets Status=Processing, WorkerId, StartedAt, LastHeartbeat.
    /// Returns null when no eligible job exists.
    ///
    /// Eligibility: Status=Queued AND (RunAfter IS NULL OR RunAfter <= UtcNow)
    ///              AND Channel=@channel
    ///              AND (partitions IS NULL OR Partition IN @partitions)
    /// Ordering: Priority ASC, CreatedAt ASC
    /// </summary>
    Task<JobRecord?> DequeueAsync(
        string channel,
        IReadOnlyList<int>? partitions = null,
        CancellationToken ct = default);

    /// <summary>
    /// Transition a job to Completed or Failed.
    /// For Failed with retries remaining, also sets RunAfter for backoff.
    /// Throws InvalidJobStatusTransitionException on illegal transition.
    /// </summary>
    Task UpdateStatusAsync(
        Guid jobId,
        JobStatus newStatus,
        string? resultJson = null,
        string? errorMessage = null,
        string? errorType = null,
        CancellationToken ct = default);

    /// <summary>
    /// Fetch the current status and metadata of a job.
    /// Returns null if jobId is unknown.
    /// </summary>
    Task<JobRecord?> GetStatusAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>
    /// Renew the heartbeat timestamp for an in-progress job.
    /// Called periodically by the HeartbeatService.
    /// No-op (no throw) if job is no longer in Processing status.
    /// </summary>
    Task HeartbeatAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>
    /// Reclaim jobs whose LastHeartbeat is older than (UtcNow - staleTimeout).
    /// Transitions them from Processing → Queued and clears WorkerId.
    /// Returns the count of jobs reclaimed.
    /// </summary>
    Task<int> ReclaimStaleJobsAsync(TimeSpan staleTimeout, CancellationToken ct = default);

    /// <summary>
    /// Move a Failed job back to Queued (manual retry trigger from API).
    /// Also accepted from DeadLettered for explicit re-queue.
    /// </summary>
    Task RequeueAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>
    /// Request cancellation of a Queued or Processing job.
    /// Queued → Cancelled immediately.
    /// Processing → sets a cancellation flag; worker polls and honours it.
    /// </summary>
    Task CancelAsync(Guid jobId, CancellationToken ct = default);
}
```

---

## 4. Dequeue Semantics

The dequeue operation must atomically claim a job — no two workers may claim the same job under concurrency. This document defines the invariant and the observable behaviour (status transition to `Processing`, worker metadata updates, eligibility rules). Concrete providers implement the atomicity using their native primitives; those details are out of scope for this core reference.

---

## 4. Heartbeat and Stale Job Recovery

### HeartbeatService

`HeartbeatService` runs as a parallel loop inside the worker process. It is decoupled from job execution and runs on its own timer.

```
HeartbeatService (runs per active job)
  Loop every HeartbeatInterval (default: 30s):
    foreach (jobId in _activeJobs):
      await _store.HeartbeatAsync(jobId)
```

The set of active jobs is maintained by `WorkerConcurrencyManager`, which adds a `jobId` when a handler starts and removes it when it completes or fails.

### Sweeper — `ReclaimStaleJobsAsync`

The sweeper runs as a separate low-priority `BackgroundService` and periodically scans for jobs whose heartbeat has lapsed.

```
SweeperService
  Loop every SweeperInterval (default: 30s):
    count = await _store.ReclaimStaleJobsAsync(StaleJobTimeout)  // default: 60s
    if count > 0: log.Warning("Reclaimed {count} stale jobs")
```

Provider implementations of `ReclaimStaleJobsAsync` perform this transition efficiently using store-native mechanisms. The exact query/command is provider-specific and not defined here.

### Why 60s Timeout?

The heartbeat fires every 30s. A job is considered stale after 60s (two missed heartbeats). This gives a generous buffer for:

- GC pauses in .NET
- Transient database latency
- Momentary network hiccups

Operators can tighten this for latency-sensitive systems (`StaleJobTimeout = 20s`, `HeartbeatInterval = 8s`).

---

## 5. Retry and Dead-Letter Mechanics

### RetryHandler

When a job handler throws, `RetryHandler` decides what to do next:

```csharp
internal sealed class RetryHandler
{
    public async Task HandleFailureAsync(JobRecord job, Exception ex, IJobStore store)
    {
        job.RetryCount++;

        if (job.RetryCount >= job.MaxRetries)
        {
            // No retries left — dead-letter
            await store.UpdateStatusAsync(
                job.JobId,
                JobStatus.DeadLettered,
                errorMessage: ex.Message,
                errorType: ex.GetType().Name);
            return;
        }

        // Compute backoff delay
        var delay = ComputeBackoff(job.RetryCount);
        job.RunAfter = DateTimeOffset.UtcNow + delay;

        // Transition to Failed first (for auditing), then re-queue
        await store.UpdateStatusAsync(
            job.JobId,
            JobStatus.Failed,
            errorMessage: ex.Message,
            errorType: ex.GetType().Name);
        await store.RequeueAsync(job.JobId);  // sets RunAfter, transitions Failed → Queued
    }

    private static TimeSpan ComputeBackoff(int attempt)
    {
        // Exponential backoff with jitter: base 5s, max 5min
        var baseSeconds = 5 * Math.Pow(2, attempt - 1);   // 5, 10, 20, 40, ...
        var capped      = Math.Min(baseSeconds, 300);      // cap at 5 minutes
        var jitter      = Random.Shared.NextDouble() * 0.2 * capped; // ±20% jitter
        return TimeSpan.FromSeconds(capped + jitter);
    }
}
```

### Backoff Schedule (defaults)

| Attempt | Base Delay | With Jitter (approx.) |
| ------- | ---------- | --------------------- |
| 1       | 5s         | 5–6s                  |
| 2       | 10s        | 10–12s                |
| 3       | 20s        | 20–24s                |
| 4       | 40s        | 40–48s                |
| 5+      | 300s (cap) | 300–360s              |

### Dead-Letter Queue Behaviour

Dead-lettered jobs are not deleted. They remain in the `async_endpoints_jobs` table with `Status=4` and can be:

- Queried via `GET /jobs?status=dead-lettered`
- Individually re-queued via `POST /jobs/{id}/requeue` (transitions `DeadLettered → Queued`)
- Bulk re-queued by channel or job type via admin API

The `result_json`, `error_message`, and `error_type` fields are preserved so operators can diagnose why the job failed.

---

## 6. JobDescriptor — Enqueue Input

`JobDescriptor` is what the caller passes to `IJobStore.EnqueueAsync`. It is separate from `JobRecord` to enforce the distinction between input data and stored state.

```csharp
public sealed record JobDescriptor
{
    public required string JobType     { get; init; }
    public required string PayloadJson { get; init; }
    public required string PayloadType { get; init; }
    public string  Channel    { get; init; } = "default";
    public int     Priority   { get; init; } = 5;
    public int?    Partition  { get; init; }
    public int     MaxRetries { get; init; } = 3;
    public DateTimeOffset? RunAfter { get; init; }
}
```

`JobSubmitter` creates a `JobDescriptor` from the user-facing `SubmitAsync<TJob>(TJob job, ...)` call using the AOT-safe serializer registry (no reflection):

```csharp
public async Task<JobSubmitResult> SubmitAsync<TJob>(
    TJob job,
    string? channel = null,
    int? priority = null,
    object? partitionBy = null,
    CancellationToken ct = default)
    where TJob : class
{
    var descriptor = new JobDescriptor
    {
        JobType     = typeof(TJob).Name,
        PayloadJson = _serializers.Serialize(job),
        PayloadType = typeof(TJob).Name,
        Channel     = channel  ?? _options.DefaultChannel,
        Priority    = priority ?? _options.DefaultPriority,
        Partition   = partitionBy != null
                        ? PartitionResolver.Resolve(partitionBy, _options.PartitionCount)
                        : null,
        MaxRetries  = _options.MaxRetries,
    };

    var jobId = await _store.EnqueueAsync(descriptor, ct);
    if (_notifier != null)
        await _notifier.NotifyJobAvailableAsync(descriptor.Channel, ct);

    return new JobSubmitResult(jobId, descriptor.Channel);
}
```

---

## 7. Payload Serialization and Handler Dispatch (AOT-Safe)

All serialization and dispatch paths are AOT-safe — no runtime type discovery, no `dynamic`, no generic construction at runtime.

### 8a. JsonSerializerContext + Serializer Registry

`System.Text.Json` source generation provides `JsonTypeInfo<T>` for each job contract. The library stores delegates built from these `JsonTypeInfo<T>` instances in a serializer registry used by both enqueue and dispatch.

```csharp
internal sealed class JobSerializerRegistry
{
    private readonly Dictionary<string, Func<object, string>> _serializers = new();
    private readonly Dictionary<string, Func<string, object>> _deserializers = new();

    public void Register<TJob>(JsonTypeInfo<TJob> jsonTypeInfo) where TJob : class
    {
        var key = typeof(TJob).Name;
        _serializers[key]   = obj => JsonSerializer.Serialize((TJob)obj, jsonTypeInfo);
        _deserializers[key] = json => JsonSerializer.Deserialize(json, jsonTypeInfo)
                                ?? throw new JobDeserializationException(key, json);
    }

    public string Serialize<TJob>(TJob job) where TJob : class
        => _serializers[typeof(TJob).Name](job);

    public object Deserialize(string jobTypeKey, string json)
        => _deserializers[jobTypeKey](json);
}
```

### 8b. JobTypeRegistry + IJobExecutor

At startup we register a factory for each job type that can execute the handler without knowing `TJob` at dispatch time.

```csharp
public interface IJobExecutor
{
    Task ExecuteAsync(JobRecord record, CancellationToken ct);
}

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
        _factories[key] = sp =>
        {
            var handler = sp.GetRequiredService<IJobHandler<TJob>>();
            return new JobExecutor<TJob>(handler, deserialize);
        };
    }

    public IJobExecutor Resolve(string jobTypeKey, IServiceProvider sp)
        => _factories.TryGetValue(jobTypeKey, out var f)
           ? f(sp)
           : throw new UnknownJobTypeException(jobTypeKey);
}
```

### 8c. Dispatcher (dictionary lookup only)

```csharp
internal sealed class JobDispatcher
{
    private readonly JobTypeRegistry _registry;
    private readonly IServiceProvider _sp;

    public JobDispatcher(JobTypeRegistry registry, IServiceProvider sp)
    { _registry = registry; _sp = sp; }

    public Task DispatchAsync(JobRecord record, CancellationToken ct)
        => _registry.Resolve(record.JobType, _sp).ExecuteAsync(record, ct);
}
```

### 8d. Registration

Applications register job types with their source-generated `JsonTypeInfo<T>`:

```csharp
// Application
[JsonSerializable(typeof(SendEmailJob))]
public partial class MyJobsJsonContext : JsonSerializerContext { }

builder.Services
    .AddAsyncEndpoints(o => o.UseConfiguredStore())
    .AddJobType<SendEmailJob>(MyJobsJsonContext.Default.SendEmailJob);
```

For convenience, a Roslyn source generator can emit the `AddJobType<T>` and handler DI registrations automatically. No assembly scanning is used.

---

## 8. WorkerId Generation

Each worker instance needs a stable, unique `WorkerId` for the duration of its lifetime. It should be:

- Unique across machines and processes
- Human-readable for debugging
- Stable within a process restart

```csharp
internal static class WorkerIdentity
{
    public static string Generate()
    {
        var host    = Environment.MachineName;
        var pid     = Environment.ProcessId;
        var suffix  = Guid.NewGuid().ToString("N")[..8]; // 8-char collision-breaker
        return $"{host}:{pid}:{suffix}";
        // e.g. "PROD-WEB-01:12345:a3f7c1d2"
    }
}
```

The `WorkerId` is set once at `JobWorkerService` startup and reused for all jobs claimed by that process.

---

## 9. Result and Error Contracts

### JobSubmitResult

```csharp
public sealed record JobSubmitResult(
    Guid   JobId,
    string Channel,
    string StatusUrl  // e.g. "/jobs/{jobId}"
);
```

### JobStatusResponse (API layer)

```csharp
public sealed record JobStatusResponse
{
    public Guid      JobId       { get; init; }
    public string    JobType     { get; init; } = string.Empty;
    public string    Channel     { get; init; } = string.Empty;
    public JobStatus Status      { get; init; }
    public int       RetryCount  { get; init; }
    public int       MaxRetries  { get; init; }
    public DateTimeOffset  CreatedAt   { get; init; }
    public DateTimeOffset? StartedAt   { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string?   ResultJson  { get; init; }  // null until Completed
    public string?   ErrorMessage { get; init; } // null unless Failed/DeadLettered
}
```

### Exception Hierarchy

```
AsyncEndpointsException                   (base)
├── InvalidJobStatusTransitionException   (illegal state machine transition)
├── UnknownJobTypeException               (job type key not registered)
├── JobDeserializationException           (payload JSON could not be parsed)
├── JobNotFoundException                  (GetStatusAsync on unknown jobId)
└── PartitionLeaseException               (lease acquisition / renewal failure)
```

---

## 10. Key Constants and Defaults

All defaults are defined on `AsyncEndpointsDefaults` so they appear in one place and can be overridden via `AsyncEndpointsOptionsBuilder`.

```csharp
internal static class AsyncEndpointsDefaults
{
    public const string DefaultChannel        = "default";
    public const int    DefaultPriority       = 5;
    public const int    DefaultMaxRetries     = 3;
    public const int    DefaultMaxConcurrency = 4;
    public const int    DefaultPartitionCount = 16;

    public static readonly TimeSpan HeartbeatInterval   = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan StaleJobTimeout     = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan SweeperInterval     = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan PollingMinInterval  = TimeSpan.FromMilliseconds(50);
    public static readonly TimeSpan PollingMaxInterval  = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan LeaseRenewalInterval = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan LeaseTimeout         = TimeSpan.FromSeconds(30);
}
```

---

## 11. Provider Boundaries

This core reference defines contracts and invariants only. Storage schemas, migration strategies, and notification mechanisms are provider concerns and will be documented with each provider. The worker engine adapts to both polling-based and event-driven providers via `IJobListener`/`IJobNotifier` abstractions defined in 001.

## Summary

The implementation is anchored by three non-negotiable invariants:

1. **Dequeue is always atomic** — providers must guarantee single-claim semantics under concurrency; the engine relies on this invariant for at-most-once delivery per job.
2. **State transitions are guarded** — no code path may write an arbitrary status; every transition goes through `AssertTransitionAllowed`.
3. **Heartbeats protect against lost work** — if a worker dies silently, the sweeper returns its jobs to the queue within `StaleJobTimeout`, bounded by two missed heartbeat cycles.

Everything else — channels, priorities, partitioning, retries — layers on top of these three guarantees without weakening them.
