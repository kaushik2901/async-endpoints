# 🧭 0. Guiding Principles (apply to ALL tasks)

Every task must adhere to:

- State transitions must go through guard (`AssertTransitionAllowed`)
- No reflection / dynamic (AOT-safe)
- Atomic dequeue invariant must not be violated
- Prefer real implementations in tests (InMemory store)

---

# 🧱 PHASE 1 — Abstractions (Foundation Layer)

## Task 1.1 — Implement Core Contracts

### Scope

Implement:

- `JobRecord`
- `JobDescriptor`
- `JobStatus`
- `IJobStore`
- `IJobHandler<T>`
- `IJobSubmitter`

### Acceptance Criteria

**Functional**

- All properties exactly match spec
- Default values correctly initialized
- `JobStatus` enum values are stable (no reordering later)

**Unit Tests**

- Creating `JobRecord` sets correct defaults (Status=Queued, RetryCount=0, timestamps set)
- `JobDescriptor` immutability verified
- Serialization compatibility (basic JSON roundtrip)

---

## Task 1.2 — State Transition Guard

### Scope

Implement:

- `AssertTransitionAllowed(from, to)`
- `InvalidJobStatusTransitionException`

### Acceptance Criteria

**Functional**

- Only allowed transitions pass (as per table)
- All invalid transitions throw

**Unit Tests (CRITICAL)**

- Exhaustive matrix test:
  - Iterate all `(from, to)` combinations
  - Assert only valid ones succeed

- Negative tests:
  - `Completed → Processing` throws
  - `Queued → Completed` throws

👉 This test suite must act as **state machine spec enforcement**

---

# ⚙️ PHASE 2 — Core (Submission + Serialization + Dispatch)

## Task 2.1 — JobSerializerRegistry

### Scope

- Implement AOT-safe serializer registry
- Register + serialize + deserialize

### Acceptance Criteria

**Functional**

- Uses `JsonTypeInfo<T>` only (no reflection)
- Key = `typeof(TJob).Name`

**Unit Tests**

- Register multiple job types → correct dispatch
- Deserialize invalid JSON → throws `JobDeserializationException`
- Unknown job type → fails deterministically

---

## Task 2.2 — JobTypeRegistry + Executor

### Scope

- Implement:
  - `JobTypeRegistry`
  - `JobExecutor<T>`
  - `IJobExecutor`

### Acceptance Criteria

**Functional**

- Resolves correct handler via DI
- No generic reflection usage

**Unit Tests**

- Register 2 job types → correct handler invoked
- Unknown job type → throws `UnknownJobTypeException`
- Handler receives correctly deserialized payload

---

## Task 2.3 — JobDispatcher

### Scope

- Dispatch using registry only

### Acceptance Criteria

**Unit Tests**

- Dispatch calls correct handler
- CancellationToken flows through
- Failure propagates exception

---

## Task 2.4 — JobSubmitter

### Scope

- Build `JobDescriptor`
- Call `IJobStore.EnqueueAsync`
- Notify (optional notifier)

### Acceptance Criteria

**Functional**

- Correct defaults applied (channel, priority, retries)
- Partition resolution works

**Unit Tests**

- Descriptor mapping correctness
- Partition hashing deterministic
- Notifier invoked only when present

---

# 🔄 PHASE 3 — Worker Engine

## Task 3.1 — JobExecutionPipeline

### Scope

- Execute job lifecycle:
  - Dequeue → Execute → UpdateStatus

### Acceptance Criteria

**Functional**

- Success → `Completed`
- Exception → `RetryHandler` invoked

**Unit Tests**

- Happy path → Completed
- Exception → Failed + retry scheduled
- Cancellation respected

---

## Task 3.2 — RetryHandler

### Scope

- Implement retry + backoff + dead-letter

### Acceptance Criteria

**Functional**

- RetryCount increments
- Backoff applied
- DeadLetter when limit reached

**Unit Tests**

- Retry path → Failed → Queued
- MaxRetries reached → DeadLettered
- Backoff increases exponentially
- Jitter within ±20%

---

## Task 3.3 — WorkerConcurrencyManager

### Scope

- Track active jobs
- Enforce max concurrency

### Acceptance Criteria

**Unit Tests**

- Concurrency limit enforced
- Add/remove job tracking works
- Thread-safe behavior (multi-thread test)

---

## Task 3.4 — HeartbeatService

### Scope

- Periodically heartbeat active jobs

### Acceptance Criteria

**Unit Tests**

- Heartbeat called for active jobs
- Removed jobs no longer heartbeated
- No exception if job no longer processing

---

## Task 3.5 — JobWorkerService

### Scope

- Background loop using `IJobListener`

### Acceptance Criteria

**Functional**

- Uses listener abstraction correctly
- Stops gracefully on cancellation

**Unit Tests**

- Loop processes jobs
- Stops on cancellation
- Does not dequeue when concurrency full

---

# 📡 PHASE 4 — Listener Layer

## Task 4.1 — PollingJobListener

### Scope

- Adaptive polling

### Acceptance Criteria

**Functional**

- Exponential backoff behavior

**Unit Tests**

- No job → interval increases
- Job found → interval resets
- Delay boundaries respected

---

## Task 4.2 — EventDrivenJobListener

### Scope

- Wait for signal → dequeue

### Acceptance Criteria

**Unit Tests**

- Wait called before dequeue
- No busy looping
- Handles spurious wakeups

---

# 🧠 PHASE 5 — Channels & Partitioning

## Task 5.1 — ChannelManager

### Scope

- Channel configuration
- Weighted scheduling

### Acceptance Criteria

**Unit Tests**

- Weighted round robin correctness
- Channel isolation enforced

---

## Task 5.2 — PartitionResolver

### Scope

- Hash-based partitioning

### Acceptance Criteria

**Unit Tests**

- Same key → same partition
- Distribution across partitions
- Stable across runs

---

## Task 5.3 — LeaseBasedPartitionAssigner

### Scope

- Partition leasing logic

### Acceptance Criteria

**Unit Tests**

- Only one worker owns partition
- Lease expiry allows reassignment
- Balanced rebalance works

---

## Task 5.4 — Partition Concurrency Control

### Scope

- Semaphore per partition

### Acceptance Criteria

**Unit Tests**

- Same partition → serialized execution
- Different partitions → parallel execution

---

# 🗄️ PHASE 6 — InMemory Provider (Reference Implementation)

## Task 6.1 — InMemoryJobStore

### Scope

- Full `IJobStore` implementation

### Acceptance Criteria

**CRITICAL (Core Invariants)**

- Dequeue is atomic (no duplicate claims)
- Status transitions validated

**Unit Tests (VERY IMPORTANT)**

- Concurrent dequeue:
  - 10 threads → each job processed once

- RunAfter respected
- Priority ordering correct
- Channel filtering correct
- Partition filtering correct

---

## Task 6.2 — InMemoryJobNotifier

### Scope

- Semaphore-based signaling

### Acceptance Criteria

**Unit Tests**

- Notify → releases waiters
- Multiple waiters behave correctly

---

# 🌐 PHASE 7 — ASP.NET Layer

## Task 7.1 — Job Endpoints

### Scope

- POST enqueue
- GET status
- POST cancel

### Acceptance Criteria

**Unit Tests**

- Enqueue returns 202 + JobId
- Status endpoint reflects transitions
- Cancel works for Queued + Processing

---

# 🧪 PHASE 8 — Cross-Cutting Test Suites (CRITICAL QUALITY LAYER)

## Task 8.1 — Concurrency Torture Tests

### Scope

Simulate:

- Multiple workers
- Multiple threads
- High contention

### Acceptance Criteria

- No duplicate processing
- No lost jobs
- No invalid transitions

---

## Task 8.2 — State Machine Invariant Tests

### Scope

- Global invariant enforcement

### Acceptance Criteria

- No test can bypass transition guard
- All store implementations must pass same suite

---

## Task 8.3 — Retry + Backoff Determinism

### Scope

- Inject time + randomness

### Acceptance Criteria

- Deterministic retry behavior in tests
- Backoff predictable when seeded

---

## Task 8.4 — AOT Compliance Tests

### Scope

- Ensure no reflection usage

### Acceptance Criteria

- Build with trimming enabled succeeds
- No runtime failures in serialization/dispatch

---

# 🧱 PHASE 9 — DI & Configuration

## Task 9.1 — ServiceCollectionExtensions

### Scope

- Register core services
- Select listener type

### Acceptance Criteria

**Unit Tests**

- With notifier → EventDrivenListener selected
- Without notifier → PollingListener selected
- All required services registered

---

# 🧩 PHASE 10 — Packaging & Structure Compliance

## Task 10.1 — Project Separation

### Scope

Ensure structure matches:

- Abstractions
- Core
- Worker
- AspNetCore
- Providers

### Acceptance Criteria

- No circular dependencies
- Providers depend only on abstractions
- Core contains no provider logic

---

# 🚨 Final Critical Acceptance Checklist

Before “done”:

### Invariants (NON-NEGOTIABLE)

- [ ] Atomic dequeue proven via concurrency tests
- [ ] State transitions 100% guarded
- [ ] No reflection/dynamic anywhere
- [ ] Retry + dead-letter behavior correct
- [ ] Heartbeat + sweeper works

### Test Quality

- [ ] ≥90% coverage on Core + Worker
- [ ] Concurrency tests present
- [ ] Deterministic tests (time/random controlled)
- [ ] No excessive mocking (prefer InMemory)

---

# 💡 Final Advice (Hard Truth)

This architecture is strong—but only if the tests are equally strong.

---
