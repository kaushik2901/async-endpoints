# Channels & Priorities and Per-Entity Ordering

## Part 1: Channels & Priorities

### The Problem They Solve

Without channels, all jobs share a single queue. This creates two failure modes:

**Starvation** — If you enqueue 10,000 report generation jobs, they block the 3 urgent password-reset emails sitting behind them. Both job types compete for the same workers.

**Unfair resource allocation** — A report job that takes 30 seconds hogs a worker slot that 60 email jobs could have used in the same time.

Channels and priorities are two orthogonal solutions to this:

- **Channels** = separate queues with dedicated worker pools
- **Priorities** = ordering within a single queue/channel

---

### Channels in Depth

#### What a Channel Is

A channel is a named logical queue. Jobs are tagged with a channel name at enqueue time, and workers subscribe to specific channels. The dequeue query adds a `WHERE Channel = @channel` filter, so workers on the `email` channel never accidentally pick up a `reports` job.

```
Queue (Physical Store)
├── channel: "email"    ──► Worker Pool A (concurrency: 10)
├── channel: "reports"  ──► Worker Pool B (concurrency: 2)
└── channel: "webhooks" ──► Worker Pool C (concurrency: 5)
```

Each channel gets its own `BackgroundService` worker pool with its own `maxConcurrency` setting. This is key — it means a burst of report jobs can saturate Worker Pool B completely, and Worker Pool A keeps humming along unaffected.

#### Channel Configuration

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.UsePostgres(connectionString);

    options.Channels(ch =>
    {
        ch.Add("email",    concurrency: 10, maxRetries: 5);
        ch.Add("reports",  concurrency: 2,  maxRetries: 1);
        ch.Add("webhooks", concurrency: 8,  maxRetries: 10);
    });
});
```

Internally, this spins up one `JobWorkerService` (BackgroundService) per channel, each configured with its own concurrency semaphore and subscribed to the correct channel filter.

#### Enqueueing to a Channel

```csharp
await submitter.SubmitAsync(job, channel: "email");
```

The `channel` is stored on the `JobRecord`. No routing logic happens at enqueue — the channel is just metadata. The workers self-select by filtering on dequeue.

#### Weighted Channel Consumption (Tier 2.5)

A single worker pool can also subscribe to _multiple_ channels with weights. This is useful when you don't want dedicated pools but still want proportional fairness:

```csharp
ch.Add("email",   weight: 3);  // pull 3 email jobs
ch.Add("reports", weight: 1);  // for every 1 report job
```

The worker's dequeue loop cycles through channels proportionally. This is implemented as a weighted round-robin: for every 4 dequeue calls, 3 go to `email` and 1 to `reports`. The `WeightedChannelSelector` holds a pre-built rotation list like `["email", "email", "email", "reports"]` and advances an index per tick.

---

### Priorities in Depth

#### What Priority Does

Priority controls _ordering within a channel_. All jobs in the `email` channel compete for the same 10 worker slots, but a Priority 1 (urgent) password reset jumps ahead of a Priority 5 (bulk) marketing email.

#### How the Dequeue Query Changes

The `JobRecord` carries a `Priority` integer (lower = more urgent, or whatever convention you define). The dequeue query adds an `ORDER BY`:

**SQL Server / Postgres:**

```sql
-- Simplified view of the atomic dequeue
SELECT TOP 1 *
FROM Jobs
WHERE Channel = @channel
  AND Status = 'Queued'
ORDER BY Priority ASC, CreatedAt ASC   -- priority first, then FIFO within same priority
FOR UPDATE SKIP LOCKED;                -- Postgres: skip rows locked by other workers
```

The `SKIP LOCKED` on Postgres is what makes this scale — competing workers don't block each other, they just skip to the next available row.

**Redis:**
A sorted set is used, scored by a composite value that encodes both priority and timestamp:

```
score = (priority * 10^13) + unixTimestampMs
```

This means lower priority numbers sort first, and within the same priority, older jobs sort first. `ZPOPMIN` atomically pops the highest-priority (lowest score) job.

#### The FIFO Guarantee Within a Priority

Jobs with the same priority level are processed in enqueue order (`CreatedAt ASC`). So Priority 1 jobs form their own FIFO sub-queue, Priority 2 jobs form another, etc. You get strict ordering within a tier without global ordering guarantees across tiers.

---

## Part 2: Per-Entity Ordering

### The Problem It Solves

Channels and priorities don't help when you need **serial processing per entity across concurrent workers**. Consider:

- `OrderPlaced(orderId: 42)` → must run before `OrderShipped(orderId: 42)` → must run before `OrderClosed(orderId: 42)`
- But `OrderPlaced(orderId: 99)` can run _concurrently_ with all of the above

With a flat queue and 10 workers, there's no guarantee that two workers don't pick up `OrderPlaced(42)` and `OrderShipped(42)` simultaneously and race.

This is the **per-entity ordering problem** — serial within an entity, parallel across entities.

---

### How Hash-Based Partitioning Solves It

#### Step 1: Hashing Jobs to Partitions

At enqueue time, the entity key is hashed to a fixed partition:

```csharp
await submitter.SubmitAsync(job, partitionBy: order.OrderId);
```

Internally:

```
partition = Math.Abs(orderId.GetHashCode()) % PartitionCount
// e.g., PartitionCount=16, orderId=42 → partition 6
//        orderId=99 → partition 11
```

The `partition` index is stored on the `JobRecord`. All jobs for the same entity always land on the same partition — that's the invariant that enables ordering.

#### Step 2: Workers Own Partitions via Leases

You can't just say "Worker A handles partitions 0–7, Worker B handles 8–15" statically — workers crash, scale in/out, and restart. Instead, each worker _leases_ partitions dynamically:

```
Lease Record (in the job store):
- PartitionId
- WorkerId (who owns it)
- LeaseExpiry (timestamp)
- LastRenewed
```

When a worker starts up, it runs a lease acquisition loop:

```
For each partition [0..15]:
    Try to atomically claim the partition lease
    (INSERT or UPDATE where LeaseExpiry < NOW())
    Collect owned partitions until maxPartitionsPerWorker is reached
```

This is a single atomic CAS-style operation per partition. Two workers racing to claim partition 6 — only one wins. The other moves on.

#### Step 3: Workers Only Dequeue from Owned Partitions

The dequeue query now filters on both channel AND owned partitions:

```sql
SELECT TOP 1 * FROM Jobs
WHERE Channel = @channel
  AND Partition IN (6, 7, 2, 14)   -- this worker's leased partitions
  AND Status = 'Queued'
ORDER BY Priority ASC, CreatedAt ASC
FOR UPDATE SKIP LOCKED;
```

Since only one worker owns partition 6 at a time, jobs for `orderId=42` are always dequeued by the same worker — serial execution guaranteed.

#### Step 4: Per-Partition Semaphores for In-Process Ordering

Even within a single worker, concurrency must be controlled per partition (not globally). A worker might own partitions 6 and 11 and want to process them in parallel — but never two jobs from partition 6 simultaneously.

```
Worker (owns partitions: 6, 11, 2, 14)
├── Partition 6  ─ SemaphoreSlim(1) ──► OrderPlaced(42) → OrderShipped(42) [serial]
├── Partition 11 ─ SemaphoreSlim(1) ──► OrderPlaced(99) [runs concurrently with above]
├── Partition 2  ─ SemaphoreSlim(1) ──► ...
└── Partition 14 ─ SemaphoreSlim(1) ──► ...
```

Each owned partition gets its own `SemaphoreSlim(1)` — a mutex. The worker acquires the partition's semaphore before processing a job and releases it after. This serializes jobs within a partition while allowing cross-partition parallelism.

#### Step 5: Rebalancing on Worker Join/Leave/Crash

When a worker crashes, its leases expire (because `HeartbeatAsync` stops renewing them). The sweeper (`ReclaimStaleJobsAsync`) runs periodically and:

1. Finds partitions whose `LeaseExpiry < NOW()`
2. Marks those partitions as available
3. Other workers racing to acquire them win via the same atomic lease claim

When a new worker joins, it competes for unclaimed or stale partitions immediately. The `RebalanceStrategy.Balanced` option goes further — if an existing worker holds more than `totalPartitions / workerCount` partitions, it voluntarily yields some so the new worker gets a fair share.

```
Before new worker joins:
Worker A: partitions [0,1,2,3,4,5,6,7]
Worker B: partitions [8,9,10,11,12,13,14,15]

After Worker C joins (Balanced rebalance):
Worker A: partitions [0,1,2,3,4,5]
Worker B: partitions [8,9,10,11,12,13]
Worker C: partitions [6,7,14,15]       ← yielded by A and B
```

Voluntary yield is a `DELETE` or expiry of the lease record. The yielding worker stops dequeuing from those partitions before releasing the lease, ensuring in-flight jobs finish cleanly.

---

### Configuration

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.UsePostgres(connectionString);

    options.UsePartitioning(p =>
    {
        p.PartitionCount = 16;           // fixed forever — changing this rehashes entities
        p.Rebalance = RebalanceStrategy.Balanced;
        p.LeaseRenewalInterval = TimeSpan.FromSeconds(10);
        p.LeaseTimeout = TimeSpan.FromSeconds(30);
    });
});
```

> ⚠️ `PartitionCount` must be treated as immutable after the first deployment. Changing it would re-hash all entity IDs to different partitions, breaking ordering guarantees for in-flight jobs. Pick a value large enough for your expected peak worker count (16 or 32 is a good default).

---

### How the Three Levels Compose

These features layer cleanly on top of each other:

```
Level 1: Channels       → isolates job types
Level 2: Priorities     → orders jobs within a channel
Level 3: Partitioning   → serializes jobs per entity within a channel+priority queue
```

A job can simultaneously have a channel (`"orders"`), a priority (`1`), and a partition key (`orderId`). The dequeue query honors all three:

```sql
WHERE Channel = 'orders'
  AND Partition IN (6, 11, 2, 14)
  AND Status = 'Queued'
ORDER BY Priority ASC, CreatedAt ASC
```

The worker processes the highest-priority, oldest job from any of its owned partitions — while ensuring no two jobs for the same partition run concurrently.
