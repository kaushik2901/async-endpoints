# AsyncJobKit - High level Design

## Core Architecture

There are three fundamental pillars:

### 1. Job Submission Layer (API Side)

`HTTP Request --> Controller/Endpoint --> IJobStore.EnqueueAsync(job) --> Return 202-Accepted { jobId, status: "Queued", url: "/jobs/{id}" }`

- The caller gets back a `jobId` and a polling URL immediately.
- A status endpoint (`GET /jobs/{id}`) lets callers check progress (Queued -> Processing -> Completed/Failed).

### 2. Job Storage Abstraction (The Pluggable Core)

**IJobStore**

- `EnqueueAsync(JobDescriptor) -> JobId`
- `DequeueAsync(channel, partitions) -> JobRecord`
- `UpdateStatusAsync(jobId, status, result?)`
- `GetStatusAsync(jobId) -> JobStatus`
- `HeartbeatAsync(jobId)` // worker liveness
- `ReclaimStaleJobsAsync(timeout)` // sweeper reclaims dead workers' jobs

**Implementations:**

- `'SqlServerJobStore'` (durable, transactional, row-level locking for dequeue)
- `'PostgresJobStore'` (same, uses `SELECT ... FOR UPDATE SKIP LOCKED` -- excellent for competing consumers)
- `'RedisJobStore'` (fast, uses Redis Streams or sorted sets)
- `'InMemoryJobStore'` (dev/test, `ConcurrentQueue` + `ConcurrentDictionary`)

The **dequeue** operation is the most critical piece. It must be **atomic** — only one worker gets a given job.

| Provider   | Dequeue Mechanism                                                                                            |
| ---------- | ------------------------------------------------------------------------------------------------------------ |
| SQL Server | `UPDATE TOP(1) ... SET Status='Processing' OUTPUT` with row lock hints (single atomic statement)             |
| Postgres   | `BEGIN; SELECT ... FOR UPDATE SKIP LOCKED; UPDATE SET Status='Processing'; COMMIT;` (atomic via transaction) |
| Redis      | `BLMOVE \| Redis Streams XREADGROUP` (atomic blocking pop)                                                   |
| In-Memory  | `ConcurrentQueue.TryDequeue`                                                                                 |

### 3. Worker Processing Layer

```
JobWorkerService (BackgroundService)
    |   Poll loop / event-driven listen (via `IJobListener`)
    |   └ job = IJobListener.WaitForNextJobAsync(channel)
    |     └ IJobHandler<TJob>.HandleAsync(job)
    |       └ IJobStore.UpdateStatusAsync(Completed / Failed)
    └-------- Heartbeat loop (parallel, via `HeartbeatService`)
                |
              IJobStore.HeartbeatAsync(jobId) // prevents stale job reclaim
```

Workers are "BackgroundService" instances.
Multiple workers can run in the same process or across multiple machines -- the atomic queue guarantees no double-processing.

## Worker Notification Abstraction (Event-Driven vs Polling)

Not all stores have native push/notification capabilities:

| Provider   | Native Notification?                                            |
| ---------- | --------------------------------------------------------------- |
| Redis      | Yes -- "BRPOP", Streams, "XREADGROUP" (blocking), Pub/Sub       |
| Postgres   | Partial -- "LISTEN/NOTIFY" (lightweight, built-in)              |
| SQL Server | Weak -- "Service Broker" / "SqlDependency" (heavy, rarely used) |
| In-Memory  | Yes -- "SemaphoreSlim", "channel", "TaskCompletionSource"       |

The abstraction can accommodate both event-driven and polling models.

### Layer 1: 'JobNotifier' (Provider-Side Signal)

```text
IJobNotifier
└ NotifyJobAvailableAsync(channel) // called by enqueue side
  └ WaitForJobAsync(channel, timeout) // called by worker side
```

Each provider implements this differently:

- **Redis**: `"NotifyJobAvailableAsync"` does `"PUBLISH queue:{channel}"`. `"WaitForJobAsync"` does `"SUBSCRIBE"` + `await`.
  - This can be used as a `"RedisBlockingListener"` that bypasses the two-step notify+dequeue model for better efficiency (see note below).
- **Postgres**: `"NotifyJobAvailableAsync"` does `"NOTIFY job_available", "channel"`. `"WaitForJobAsync"` does `"LISTEN job_available"` + await notification.
- **SQL Server**: No good native option. Falls back to **Layer 2**.
- **In-Memory**: `"NotifyJobAvailableAsync"` releases a `"SemaphoreSlim"`. `"WaitForJobAsync"` does `await semaphore.WaitAsync(timeout)`.

---

### Layer 2: Adaptive Polling fallback

For providers without native signaling (or as a universal fallback), the worker uses **adaptive polling**:

```text
AdaptivePoller
- minInterval: 50ms
- maxInterval: 5s
- currentInterval: starts at minInterval

Loop:
  job = DequeueAsync()
  if job != null:
    process(job)
    currentInterval = minInterval // reset -> queue is hot
  else:
    currentInterval = min(currentInterval * 2, maxInterval) // backoff
    await Task.Delay(currentInterval)
```

This gives near-instant pickup when the queue is busy, and low CPU/DB cost when idle.

### Unified 'IJobListener' Interface

The worker engine combines both layers with a single abstraction:

```text
IJobListener
  └── WaitForNextJobAsync(channel, cancellationToken) -> JobRecord?
```

The package ships two implementations:

- **EventDrivenJobListener** (wraps 'IJobNotifier' + 'IJobStore.Dequeue')
  - Calls 'WaitForJobAsync()' to block until signal
  - Then calls 'DequeueAsync()' to atomically claim the job
  - Used when the provider supplies an 'IJobNotifier'

- **PollingJobListener** (wraps 'IJobStore.Dequeue' only)
  - Uses AdaptivePoll loop
  - Universal fallback, with any store

> **Note on Redis**: Redis's 'BLMOVE' natively combines waiting and dequeueing into a single atomic operation.
>
> The two-step EventDrivenJobListener (wait for signal, then dequeue separately) adds unnecessary overhead for Redis.
>
> To handle this, the package can ship a 'RedisBlockingJobListener' that implements 'IJobListener' directly using 'BLMOVE', bypassing the 'IJobNotifier' abstraction entirely.
>
> This is an internal optimization -- the worker still depends on 'IJobListener' and is unaware of the implementation.

### Provider Registration Decides the Mode

```csharp
// Provider registration (pseudo-code)
services.AddAsyncJobKit(options => {
    options.UsePostgres(connString);
    // internally registers PostgresJobStore + PostgresJobNotifier
    // engine sees IJobNotifier is registered -> picks EventDrivenJobListener
});

services.AddAsyncJobKit(options => {
    options.UseSqlServer(connString);
    // registers SqlServerJobStore only, no IJobNotifier
    // engine sees no IJobNotifier -> falls back to PollingJobListener
});
```

The worker itself never knows or cares which mode it is running in:

```csharp
// Inside the BackgroundService
while (!stoppingToken.IsCancellationRequested)
{
    var job = await _jobListener.WaitForNextJobAsync(channel, stoppingToken);
    if (job != null)
    {
        await _handler.HandleAsync(job);
    }
}
```

## Scalability Design Decisions

### A. Competing Consumers (Horizontal Scaling)

- N workers across M machines all call 'DequeueAsync'. The store's atomic dequeue handles contention.
- This is the primary scale-out axis.

### B. Job Partitioning / Channels

- Jobs can have a 'channel' or 'queue name' (e.g., 'email', 'reports'). Workers subscribe to specific channels.
- This prevents a flood of cheap jobs from starving expensive ones.

### C. Heartbeat + Stale Job Recovery

- Workers periodically heartbeat. If a worker dies mid-processing, a background sweeper reclaims jobs whose heartbeat is stale (e.g., >60s).
- This avoids lost jobs without requiring distributed transactions.

### D. Backpressure

- Workers control their own concurrency ('maxConcurrentJobs' setting). When all slots are full, the worker simply stops dequeuing.
- The queue grows, and you scale out by adding workers.

### E. Priority Support

- Jobs carry a 'Priority' field. Dequeue queries order by priority first, then enqueue time.
- This is trivial in SQL ('ORDER BY Priority, CreatedAt') and in Redis (sorted sets scored by priority+timestamp).

## Job Lifecycle State Machine

```
Queued → Processing → Completed
            ↳ Failed → Queued (retry, if retries remaining)
                ↳ DeadLettered (max retries exceeded)

Each transition is an atomic store operation. Retry count and max retries live on the job record.
```

## Key Data Model

```
JobRecord:
- JobId (GUID)
- Channel (string)
- Priority (int)
- Payload (serialized JSON)
- Status (Queued | Processing | Completed | Failed | DeadLettered)
- RetryCount / MaxRetries
- CreatedAt / StartedAt / CompletedAt
- WorkerId (who claimed it)
- LastHeartbeat
- Result / ErrorMessage
```

## Job Partitioning Strategies

### Level 1: Logical Channels (Built-in)

- Jobs are tagged with a `channel` name. Workers subscribe to one or more channels. Dequeue adds a `WHERE Channel = @channel` filter.
- Prevents slow job types from starving fast ones.

### Level 2: Weighted Channel Consumption (Built-in)

- Workers assign weights to channels, controlling how many jobs from each channel they pull per cycle.
- Enables fair scheduling with a single worker pool.

### Level 3: Hash-Based Partitioning + Lease-Based Assignment (Opt-in)

- For per-entity ordering guarantees. Jobs are hashed by entity ID to a fixed partition set. Workers own partitions via leases.
- Per-partition semaphores ensure serial execution within a partition while allowing parallelism across partitions.
- Lease-based assignment handles worker join/leave/crash with automatic rebalancing.

### Level 4: Physical Store Sharding (Advanced / User-Composed)

- At extreme scale, shard the job itself across multiple database/Redis instances via a `ShardedJobStore` decorator.

### Progressive Disclosure API Design

#### Tier 1: "I just want async jobs" (3 lines)

```csharp
// Program.cs
builder.Services.AddAsyncJobKit(options =>
{
    options.UsePostgres(connectionString);
});

// Defaults: single channel, competing consumers, event-driven listener,
// 4 concurrent jobs, 3 retries, exponential backoff, heartbeat 30s
```

```csharp
// Define a job
public record SendEmailJob(string To, string Subject, string Body);

// Define a handler
public class SendEmailHandler : IJobHandler<SendEmailJob>
{
    public async Task HandleAsync(SendEmailJob job, CancellationToken ct)
    {
        await emailService.SendAsync(job.To, job.Subject, job.Body);
    }
}
```

```csharp
// Enqueue from a controller
app.MapPost("/send-email", async (SendEmailJob job, IJobSubmitter submitter) =>
{
    var result = await submitter.SubmitAsync(job);
    return Results.Accepted($"/jobs/{result.JobId}", result);
});
```

#### Tier 2: "I need channels and priorities"

```csharp
builder.Services.AddAsyncJobKit(options =>
{
    options.UsePostgres(connectionString);

    options.Channels(ch =>
    {
        ch.Add("email", priority: 1, concurrency: 10);
        ch.Add("reports", priority: 2, concurrency: 2);
    });
});

// Enqueue to a specific channel
await submitter.SubmitAsync(job, channel: "email");
```

#### Tier 3: "I need per-entity ordering"

```csharp
builder.Services.AddAsyncJobKit(options =>
{
    options.UsePostgres(connectionString);

    options.UsePartitioning(p =>
    {
        p.PartitionCount = 16;
        p.Rebalance = RebalanceStrategy.Balanced;
    });
});

// Enqueue with entity key -- framework handles hashing + routing
await submitter.SubmitAsync(job, partitionBy: order.OrderId);
```

### Options Builder

```csharp
public class AsyncJobKitOptionsBuilder
{
    // Provider (exactly one required)
    public void UsePostgres(string conn) { ... }
    public void UseSqlServer(string conn) { ... }
    public void UseRedis(string conn) { ... }
    public void UseInMemory() { ... }

    // Optional features (progressive)
    public void Channels(Action<ChannelBuilder> configure) { ... }
    public void UsePartitioning(Action<PartitionOptions> configure) { ... }

    // Tuning knobs (all have sensible defaults)
    public int MaxConcurrency { get; set; } = 4;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan StaleJobTimeout { get; set; } = TimeSpan.FromSeconds(60);
    public TimeSpan PollingMinInterval { get; set; } = TimeSpan.FromMilliseconds(50);
    public TimeSpan PollingMaxInterval { get; set; } = TimeSpan.FromSeconds(5);
}
```

### Internal Composition (Transparent to User)

```
UsePostgres + no partitioning
  -> PostgresJobStore + PostgresJobNotifier + EventDrivenJobListener

UseSqlServer + no partitioning
  -> SqlServerJobStore + PollingJobListener (no native notifier)

UseRedis + partitioning enabled
  -> RedisJobStore + RedisBlockingJobListener
     + LeaseBasedPartitionAssigner + PartitionAwareWorker
```

### Handler Auto-Discovery

```csharp
builder.Services.AddAsyncJobKit(options =>
{
    options.UsePostgres(connectionString);
    options.ScanHandlersFrom(typeof(Program).Assembly); // optional, scans entry assembly by default
});
```

Maps `IJobHandler<SendEmailJob>` to job type `"SendEmailJob"` automatically.  
Payload type name is stored at enqueue; correct handler resolved from DI at dequeue.

### Auto-Mapped Status Endpoints

```csharp
app.MapAsyncJobKitEndpoints("/jobs");

// GET /jobs/{id}         -> job status, progress, result
// GET /jobs/{int}/result  -> final result payload (when completed)
// POST /jobs/{id}/cancel -> request cancellation
```

## Summary

The entire design hinges on **one key principle**: the job store's queue must be atomic and provider-native. Everything else (submission, polling, retry, heartbeat) is provider-agnostic code that sits on top of the `IJobStore` abstraction. This keeps the package pluggable while letting each storage backend use its strongest concurrency primitive for scalability.

The consumer-facing API follows **progressive disclosure**: simple things are simple (3 lines to get started), advanced things are possible (channels, partitioning, sharding) via opt-in configuration.
