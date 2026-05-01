# AsyncEndpoints: Scalability, Strength, and Stability Design

## Executive Summary

This document outlines the architectural improvements needed to make AsyncEndpoints a production-ready, scalable, and stable library suitable for enterprise deployments. It maps the existing implementation against the design vision from `claude-analysis.md` and identifies specific enhancements required to achieve those goals.

---

## 1. Current State Assessment

### What's Implemented

| Component               | Status      | Notes                                         |
| ----------------------- | ----------- | --------------------------------------------- |
| **Core Job Model**      | ✅ Complete | Job, JobStatus, JobState transitions          |
| **InMemoryJobStore**    | ✅ Complete | ConcurrentDictionary-based, thread-safe       |
| **RedisJobStore**       | ✅ Complete | Hash-based storage, Lua scripts for atomicity |
| **JobManager**          | ✅ Complete | Submit, claim, success/failure processing     |
| **Background Services** | ✅ Complete | Main worker, recovery service                 |
| **Configuration**       | ✅ Complete | Worker, JobManager, Response, Observability   |
| **Observability**       | ✅ Complete | ActivitySource, metrics recording             |
| **HTTP Integration**    | ✅ Complete | Extensions, handlers, context preservation    |

### What's Missing or Partial

| Component                        | Status     | Priority                           |
| -------------------------------- | ---------- | ---------------------------------- |
| **IJobStore Extended Interface** | 🔶 Partial | Missing heartbeat, reclaim methods |
| **IJobNotifier**                 | ❌ Missing | Event-driven notifications         |
| **Adaptive Polling**             | 🔶 Partial | Within Redis, not abstracted       |
| **Job Partitioning**             | 🔶 Partial | Channels in model, no strategy     |
| **SQL Server Store**             | ❌ Missing | Needed for enterprise              |
| **Postgres Store**               | ❌ Missing | Needed for enterprise              |
| **IJobListener**                 | ❌ Missing | Abstraction for job acquisition    |
| **Handler Auto-Discovery**       | 🔶 Partial | Manual registration only           |
| **Dashboard**                    | ❌ Missing | Not implemented                    |
| **Recurring Jobs**               | ❌ Missing | Cron-based scheduling              |

---

## 2. Core Architecture Enhancement

### 2.1 Extended IJobStore Interface

The current interface lacks several methods required for a complete job queue system. We need to extend it:

```csharp
public interface IJobStore
{
    // Existing: Core operations
    Task<MethodResult> CreateJob(Job job, CancellationToken cancellationToken);
    Task<MethodResult<Job>> GetJobById(Guid id, CancellationToken cancellationToken);
    Task<MethodResult> UpdateJob(Job job, CancellationToken cancellationToken);
    Task<MethodResult<Job>> ClaimNextJobForWorker(Guid workerId, CancellationToken cancellationToken);
    Task<int> RecoverStuckJobs(long timeoutUnixTime, int maxRetries, CancellationToken cancellationToken);
    bool SupportsJobRecovery { get; }

    // --- NEW: Extended interface for scalability ---

    // Heartbeat for worker liveness (prevents stale job reclaim)
    Task<MethodResult> HeartbeatAsync(Guid jobId, CancellationToken cancellationToken);

    // Batch claim for throughput optimization (claim multiple jobs at once)
    Task<MethodResult<List<Job>>> ClaimNextJobsForWorker(Guid workerId, int batchSize, CancellationToken cancellationToken);

    // Reclaim jobs from dead workers (called by sweeper)
    Task<int> ReclaimStaleJobsAsync(TimeSpan timeout, int maxRetries, CancellationToken cancellationToken);

    // Get current queue depth for backpressure
    Task<int> GetQueueDepthAsync(string? channel = null, CancellationToken cancellationToken);

    // Delete completed/failed jobs older than threshold
    Task<int> PruneOldJobsAsync(TimeSpan olderThan, CancellationToken cancellationToken);
}
```

### 2.2 Job Partitioning Strategy

Implement the three-tier partitioning approach from the design:

```csharp
// Tier 1: Logical Channels
public record ChannelConfig(string Name, int Priority, int MaxConcurrency);

// Tier 2: Weighted Channel Consumption (built into worker config)
public record ChannelWeight(string Channel, double Weight);

// Tier 3: Hash-Based Partitioning
public interface IPartitionStrategy
{
    int GetPartition(string partitionKey, int partitionCount);
}

public class HashBasedPartitionStrategy : IPartitionStrategy
{
    public int GetPartition(string partitionKey, int partitionCount)
    {
        var hash = MurmurHash3.Hash(Encoding.UTF8.GetBytes(partitionKey));
        return ((hash % partitionCount) + partitionCount) % partitionCount;
    }
}
```

---

## 3. Worker Notification Abstraction

### 3.1 IJobListener Interface

Abstract the job acquisition mechanism to support both event-driven and polling models:

```csharp
public interface IJobListener
{
    Task<Job?> WaitForNextJobAsync(string? channel, CancellationToken cancellationToken);
}

public interface IJobNotifier
{
    Task NotifyJobAvailableAsync(string channel);
    Task WaitForJobAsync(string channel, CancellationToken cancellationToken);
}

// Event-driven listener (uses notifier)
public class EventDrivenJobListener : IJobListener
{
    private readonly IJobNotifier _notifier;
    private readonly IJobStore _store;
    private readonly Guid _workerId;

    public async Task<Job?> WaitForNextJobAsync(string? channel, CancellationToken ct)
    {
        await _notifier.WaitForJobAsync(channel, ct);
        return await _store.ClaimNextJobForWorker(_workerId, ct);
    }
}

// Adaptive polling fallback
public class AdaptivePollingListener : IJobListener
{
    private readonly IJobStore _store;
    private readonly Guid _workerId;
    private TimeSpan _currentInterval = TimeSpan.FromMilliseconds(50);
    private readonly TimeSpan _minInterval = TimeSpan.FromMilliseconds(50);
    private readonly TimeSpan _maxInterval = TimeSpan.FromSeconds(5);

    public AdaptivePollingListener(IJobStore store, Guid workerId)
    {
        _store = store;
        _workerId = workerId;
    }

    public async Task<Job?> WaitForNextJobAsync(string? channel, CancellationToken ct)
    {
        var result = await _store.ClaimNextJobForWorker(_workerId, ct);
        var job = result.DataOrNull;

        if (job != null)
        {
            _currentInterval = _minInterval; // Reset - queue is hot
        }
        else
        {
            _currentInterval = TimeSpan.FromSeconds(
                Math.Min(_currentInterval.TotalSeconds * 2, _maxInterval.TotalSeconds));
            await Task.Delay(_currentInterval, ct);
        }

        return job;
    }
}
```

### 3.2 Provider Detection

The system automatically selects the appropriate listener based on what's registered:

```csharp
public static class JobListenerSelector
{
    public static IJobListener Select(IServiceProvider services, Guid workerId)
    {
        // If IJobNotifier is registered, use event-driven listener
        var notifier = services.GetService<IJobNotifier>();
        if (notifier != null)
        {
            return new EventDrivenJobListener(
                notifier,
                services.GetRequiredService<IJobStore>(),
                workerId);
        }

        // Fall back to adaptive polling
        return new AdaptivePollingListener(
            services.GetRequiredService<IJobStore>(),
            workerId);
    }
}
```

---

## 4. Storage Provider Extension

### 4.1 Required Implementations

We need to add multiple storage providers to support enterprise scenarios:

| Provider          | Use Case                            | Key Feature                     |
| ----------------- | ----------------------------------- | ------------------------------- |
| **SQL Server**    | Enterprise, existing infrastructure | Row-level locking, ACID         |
| **Postgres**      | Cloud-native, open-source           | `SELECT FOR UPDATE SKIP LOCKED` |
| **Redis Streams** | High-throughput, pub/sub            | Native blocking reads           |

### 4.2 SQL Server Implementation Pattern

```csharp
public class SqlServerJobStore : IJobStore
{
    private readonly string _connectionString;

    public async Task<MethodResult<Job>> ClaimNextJobForWorker(Guid workerId, CancellationToken ct)
    {
        // Atomic claim using single UPDATE statement with OUTPUT clause
        const string sql = @"
            UPDATE TOP(1) Jobs
            SET Status = @InProgress, WorkerId = @WorkerId, StartedAt = @Now, LastUpdatedAt = @Now
            OUTPUT INSERTED.Id, INSERTED.Name, INSERTED.Status, INSERTED.Payload,
                   INSERTED.Result, INSERTED.Error, INSERTED.RetryCount,
                   INSERTED.MaxRetries, INSERTED.RetryDelayUntil, INSERTED.WorkerId,
                   INSERTED.CreatedAt, INSERTED.StartedAt, INSERTED.CompletedAt,
                   INSERTED.LastUpdatedAt
            WHERE Status = @Queued AND (RetryDelayUntil IS NULL OR RetryDelayUntil <= @Now)
            ORDER BY CreatedAt";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@InProgress", (int)JobStatus.InProgress);
        cmd.Parameters.AddWithValue("@WorkerId", workerId);
        cmd.Parameters.AddWithValue("@Now", DateTimeOffset.UtcNow);
        cmd.Parameters.AddWithValue("@Queued", (int)JobStatus.Queued);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return MethodResult<Job>.Success(MapReaderToJob(reader));
        }

        return MethodResult<Job>.Success(default!);
    }

    public async Task<int> RecoverStuckJobs(long timeout, int maxRetries, CancellationToken ct)
    {
        const string sql = @"
            UPDATE Jobs
            SET Status = @Queued, WorkerId = NULL, StartedAt = NULL,
                RetryCount = TRY_CAST(RetryCount AS INT) + 1
            WHERE Status = @InProgress
            AND LastUpdatedAt < @Timeout
            AND TRY_CAST(RetryCount AS INT) < @MaxRetries";

        // Uses row-level locking - atomic operation
    }
}
```

### 4.3 Postgres Implementation Pattern

```csharp
public class PostgresJobStore : IJobStore
{
    private readonly NpgsqlConnection _connection;

    public async Task<MethodResult<Job>> ClaimNextJobForWorker(Guid workerId, CancellationToken ct)
    {
        // Uses SELECT FOR UPDATE SKIP LOCKED - perfect for competing consumers
        await using var tx = await _connection.BeginTransactionAsync(ct);

        const string selectSql = @"
            SELECT id, name, status, headers, route_params, query_params,
                   payload, result, error, retry_count, max_retries,
                   retry_delay_until, worker_id, created_at, started_at,
                   completed_at, last_updated_at
            FROM jobs
            WHERE status = 'Queued' OR (status = 'Scheduled' AND retry_delay_until <= NOW())
            ORDER BY created_at ASC
            LIMIT 1
            FOR UPDATE SKIP LOCKED";

        await using var cmd = new NpgsqlCommand(selectSql, _connection, tx);
        var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
        {
            return MethodResult<Job>.Success(default!);
        }

        // Update status in same transaction
        const string updateSql = @"
            UPDATE jobs
            SET status = @InProgress, worker_id = @WorkerId,
                started_at = NOW(), last_updated_at = NOW()
            WHERE id = @Id";

        // Atomic via transaction
    }
}
```

### 4.4 Redis Streams (Optimized)

```csharp
public class RedisStreamJobStore : IJobStore
{
    private readonly IDatabase _database;
    private const string StreamKey = "ae:jobs:stream";

    public async Task<MethodResult<Job>> ClaimNextJobForWorker(Guid workerId, CancellationToken ct)
    {
        // Blocking read from stream - atomic, native
        var entries = await _database.StreamReadAsync(
            StreamKey,
            "0", // last-id
            count: 1,
            mode: StreamMode.Random,
            cancellationToken: ct);

        if (entries.Length == 0)
        {
            return MethodResult<Job>.Success(default!);
        }

        var entry = entries[0];
        var job = ConvertStreamEntryToJob(entry);

        return MethodResult<Job>.Success(job);
    }

    public async Task<MethodResult> CreateJob(Job job, CancellationToken ct)
    {
        // Add to stream
        var values = new NameValueEntry[]
        {
            new NameValueEntry("id", job.Id.ToString()),
            new NameValueEntry("name", job.Name),
            new NameValueEntry("payload", job.Payload),
            // ... other fields
        };

        await _database.StreamAddAsync(StreamKey, values);
        return MethodResult.Success();
    }
}
```

---

## 5. Resilience and Recovery

### 5.1 Heartbeat Mechanism

Workers must periodically heartbeat to indicate they're still processing:

```csharp
public class HeartbeatService : BackgroundService
{
    private readonly IJobStore _store;
    private readonly Guid _workerId;
    private readonly AsyncEndpointsRecoveryConfigurations _config;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_config.HeartbeatInterval, stoppingToken);

            if (stoppingToken.IsCancellationRequested) break;

            // Get all jobs being processed by this worker
            await _store.HeartbeatAsync(_workerId, stoppingToken);
        }
    }
}
```

### 5.2 Stale Job Reclamation

A background sweeper reclaims jobs from dead workers:

```csharp
public class StaleJobReclaimerService : BackgroundService
{
    private readonly IJobStore _store;
    private readonly AsyncEndpointsRecoveryConfigurations _config;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var reclaimed = await _store.ReclaimStaleJobsAsync(
                _config.StaleJobTimeout,
                _config.MaxRetries,
                stoppingToken);

            if (reclaimed > 0)
            {
                _logger.LogInformation("Reclaimed {Count} stale jobs", reclaimed);
            }

            await Task.Delay(_config.ReclaimInterval, stoppingToken);
        }
    }
}
```

### 5.3 In-Memory Store Updates for Extended Interface

Update the existing InMemoryJobStore to support the extended interface:

```csharp
public class InMemoryJobStore : IJobStore
{
    public Task<MethodResult> HeartbeatAsync(Guid jobId, CancellationToken ct)
    {
        if (!jobs.TryGetValue(jobId, out var job))
        {
            return MethodResult.Failure(...);
        }

        job.LastUpdatedAt = _dateTimeProvider.DateTimeOffsetNow;
        return MethodResult.Success();
    }

    public Task<MethodResult<List<Job>>> ClaimNextJobsForWorker(Guid workerId, int batchSize, CancellationToken ct)
    {
        var batch = jobs.Values
            .Where(j => j.WorkerId == null)
            .Where(j => j.Status == JobStatus.Queued || j.Status == JobStatus.Scheduled)
            .OrderBy(j => j.CreatedAt)
            .Take(batchSize)
            .ToList();

        // Atomic claim all in batch
        // ... implementation
    }

    public Task<int> ReclaimStaleJobsAsync(TimeSpan timeout, int maxRetries, CancellationToken ct)
    {
        var now = _dateTimeProvider.DateTimeOffsetNow;
        var stale = jobs.Values
            .Where(j => j.Status == JobStatus.InProgress)
            .Where(j => j.WorkerId != null)
            .Where(j => now - j.LastUpdatedAt > timeout)
            .ToList();

        int count = 0;
        foreach (var job in stale)
        {
            // Reset to queue with retry
            job.Status = JobStatus.Queued;
            job.WorkerId = null;
            job.RetryCount++;
            count++;
        }

        return count;
    }
}
```

---

## 6. Backpressure and Flow Control

### 6.1 Worker Concurrency Control

```csharp
public class AsyncEndpointsBackgroundService : BackgroundService
{
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly IJobListener _listener;
    private readonly IJobProcessorService _processor;

    public AsyncEndpointsBackgroundService(
        AsyncEndpointsWorkerConfigurations config,
        IJobListener listener,
        IJobProcessorService processor)
    {
        _concurrencyLimiter = new SemaphoreSlim(config.MaxConcurrentJobs);
        _listener = listener;
        _processor = processor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var slotAcquired = await _concurrencyLimiter.WaitAsync(TimeSpan.FromSeconds(1), stoppingToken);

            if (!slotAcquired)
            {
                // Backpressure: wait before trying to claim
                await Task.Delay(100, stoppingToken);
                continue;
            }

            try
            {
                var job = await _listener.WaitForNextJobAsync(_channel, stoppingToken);
                if (job != null)
                {
                    _ = ProcessWithReleaseAsync(job, stoppingToken);
                }
            }
            finally
            {
                _concurrencyLimiter.Release();
            }
        }
    }

    private async Task ProcessWithReleaseAsync(Job job, CancellationToken ct)
    {
        try
        {
            await _processor.ProcessJobAsync(job, ct);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }
}
```

### 6.2 Queue Depth Monitoring

Let workers know when to pause dequeuing:

```csharp
public class BackpressureService
{
    private readonly IJobStore _store;

    public async Task<MethodResult> EnqueueAsync(Job job)
    {
        var depth = await _store.GetQueueDepthAsync();

        if (depth > _config.MaxQueueDepth)
        {
            // Signal backpressure - caller should delay or reject
            return MethodResult.Failure(AsyncEndpointError.FromCode(
                "QUEUE_FULL",
                $"Queue depth {depth} exceeds limit {_config.MaxQueueDepth}"));
        }

        return await _store.CreateJob(job);
    }
}
```

---

## 7. Progressive Disclosure API

### 7.1 Tier 1: Simple (3 lines)

```csharp
// Program.cs
builder.Services.AddAsyncJobKit(options =>
{
    options.UsePostgres(connectionString);
});
// Defaults: single channel, competing consumers, event-driven listener,
// 4 concurrent jobs, 3 retries, exponential backoff, heartbeat 30s
```

### Tier 2: Channels and Priorities

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

### Tier 3: Partitioning

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

// Enqueue with entity key - framework handles hashing + routing
await submitter.SubmitAsync(job, partitionBy: order.OrderId);
```

---

## 8. Handler Auto-Discovery

Implement assembly scanning for handler registration:

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class AsyncEndpointAttribute : Attribute
{
    public string JobName { get; }

    public AsyncEndpointAttribute(string jobName)
    {
        JobName = jobName;
    }
}

public static class HandlerDiscoverer
{
    public static IServiceCollection DiscoverFromAssembly(
        this IServiceCollection services,
        Assembly assembly)
    {
        var handlerTypes = assembly.GetTypes()
            .Where(t => t.IsAssignableTo(typeof(IAsyncEndpointRequestHandler)))
            .Where(t => t.GetCustomAttribute<AsyncEndpointAttribute>() != null);

        foreach (var handlerType in handlerTypes)
        {
            var attr = handlerType.GetCustomAttribute<AsyncEndpointAttribute>()!;
            services.AddAsyncEndpointHandler(handlerType, attr.JobName);
            services.AddKeyedScoped(handlerType, typeof(IAsyncEndpointRequestHandler), handlerType, attr.JobName);
        }

        return services;
    }
}

// Usage
builder.Services.AddAsyncJobKit(options =>
{
    options.UsePostgres(connectionString);
    options.ScanHandlersFrom(typeof(Program).Assembly);
});
```

---

## 9. Redis Implementation Optimizations

The existing Redis implementation needs updates to support the extended interface:

### 9.1 Add Heartbeat to RedisJobStore

```csharp
public class RedisJobStore : IJobStore
{
    public async Task<MethodResult> HeartbeatAsync(Guid jobId, CancellationToken ct)
    {
        const string luaScript = @"
            local jobKey = KEYS[1]
            local now = ARGV[1]
            redis.call('HSET', jobKey, 'LastUpdatedAt', now)
            return 1
        ";

        var result = await _database.ScriptEvaluateAsync(
            luaScript,
            new { jobKey = GetJobKey(jobId), now = DateTimeOffset.UtcNow.ToString("O") });

        return MethodResult.Success();
    }

    public async Task<int> ReclaimStaleJobsAsync(TimeSpan timeout, int maxRetries, CancellationToken ct)
    {
        return await _redisLuaScriptService.ReclaimStaleJobs(_database, timeout, maxRetries);
    }
}
```

### 9.2 Batch Claim

```csharp
public async Task<MethodResult<List<Job>>> ClaimNextJobsForWorker(
    Guid workerId,
    int batchSize,
    CancellationToken ct)
{
    var jobs = new List<Job>();

    for (int i = 0; i < batchSize; i++)
    {
        var result = await ClaimNextJobForWorker(workerId, ct);
        if (result.DataOrNull == null) break;

        jobs.Add(result.DataOrNull);
    }

    return !jobs.Any()
        ? MethodResult<List<Job>>.Success(default!)
        : MethodResult<List<Job>>.Success(jobs);
}
```

---

## 10. Dashboard (Future)

For competitive parity with Hangfire, implement a web dashboard:

| Feature        | Description                       |
| -------------- | --------------------------------- |
| Job List       | Real-time job status grid         |
| Job Details    | Full payload, history, logs       |
| Recurring Jobs | Cron-scheduled job management     |
| Metrics        | Throughput, latency charts        |
| Health         | Store connectivity, worker status |

---

## 11. Implementation Roadmap

### Phase 1: Foundation (1-2 months)

- [ ] Extend IJobStore interface with heartbeat, batch claim, reclaim methods
- [ ] Implement IJobListener abstraction
- [ ] Add worker heartbeat service
- [ ] Implement stale job reclaimer
- [ ] Add backpressure controls

### Phase 2: Storage Expansion (2-3 months)

- [ ] SQL Server job store
- [ ] Postgres job store
- [ ] Connection pooling for both

### Phase 3: Enterprise Features (3-4 months)

- [ ] Handler auto-discovery
- [ ] Dashboard (MVP)
- [ ] Job priorities
- [ ] Job dependencies

### Phase 4: Competitive Features (4-6 months)

- [ ] Recurring jobs (CRON)
- [ ] Job grouping/tagging
- [ ] Performance optimization
- [ ] Migration tools from Hangfire

---

## 12. Success Criteria

### Performance Targets

| Metric            | Target               | Notes                     |
| ----------------- | -------------------- | ------------------------- |
| **Throughput**    | 10,000+ jobs/second  | Redis with optimal tuning |
| **Latency**       | < 5ms median         | Job processing start time |
| **Memory**        | < 1KB per queued job | Includes payload          |
| **Recovery Time** | < 30 seconds         | After worker crash        |

### Reliability Targets

| Metric             | Target         | Notes                         |
| ------------------ | -------------- | ----------------------------- |
| **Data Integrity** | Zero lost jobs | With proper reclamation       |
| **Duplication**    | Zero           | At-least-once delivery        |
| **Uptime**         | 99.99%         | Excluding planned maintenance |

### Operational Targets

| Metric            | Target     | Notes                   |
| ----------------- | ---------- | ----------------------- |
| **AOT Support**   | Full       | Self-contained部署      |
| **Observability** | Complete   | Traces, metrics, health |
| **Configuration** | Zero-touch | For simple deployments  |

---

## 13. Key Design Principles

1. **Atomic Dequeue**: Each provider uses its strongest primitive
   - Redis: Lua scripts
   - SQL Server: Row-level locking
   - Postgres: `SELECT FOR UPDATE SKIP LOCKED`

2. **Progressive Disclosure**: Simple 3-line API, opt-in complexity

3. **Resilience**: Heartbeat + reclamation ensures no lost jobs

4. **Extensibility**: New stores can implement the `IJobStore` interface

5. **Observability**: Built-in ActivitySource for distributed tracing

---

## 14. Conclusion

This design document provides a concrete path to making AsyncEndpoints scalable, strong, and stable. The existing implementation forms a solid foundation; adding the extended interface methods, storage providers, and resilience mechanisms will position the library for enterprise use.

The key focus areas are:

1. **Extend the IJobStore interface** with heartbeat and batch operations
2. **Implement IJobListener** for flexible job acquisition
3. **Add SQL Server and Postgres stores** for enterprise support
4. **Implement heartbeat and reclamation** for reliability
5. **Add backpressure** for queue protection

Following this roadmap will make AsyncEndpoints competitive with established solutions like Hangfire while maintaining its core strengths in HTTP integration and modern .NET architecture.

