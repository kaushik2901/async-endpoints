# 006 — Engineering Standards & Best Practices

## Overview

This document defines the **engineering standards, coding conventions, and architectural rules** for AsyncEndpoints.

These rules ensure:

- AOT compatibility is preserved
- Concurrency correctness is not compromised
- The system remains testable and maintainable
- Providers remain pluggable and isolated

> These are **non-optional guidelines** for contributors.

---

# 1. Core Philosophy

### 1.1 Prefer Explicitness Over Magic

- No hidden behavior
- No implicit conventions that require "knowing the system"
- All critical flows must be **traceable and debuggable**

---

### 1.2 AOT-Safe by Default

From :

- ❌ No reflection-based discovery
- ❌ No `dynamic`
- ❌ No runtime generic construction
- ✅ Registry-based dispatch
- ✅ Source-generated serialization

If a design breaks AOT → it is rejected.

---

### 1.3 Composition Over Inheritance

- Prefer small composable services
- Avoid deep inheritance hierarchies
- Use interfaces only where they add value (see below)

---

# 2. Interface Design Guidelines

## 2.1 DO NOT Create Interfaces “Just for Testing”

This is a **critical rule**.

### ❌ Avoid:

```csharp
public interface IFooService { ... }
public class FooService : IFooService { ... }
```

If:

- There is only one implementation
- No polymorphism is needed
- No external boundary exists

👉 This adds **unnecessary indirection**

---

## 2.2 Create Interfaces ONLY When:

### ✅ 1. It is a boundary

Examples:

- `IJobStore` (provider abstraction)
- `IJobListener` (execution model abstraction)
- `IJobNotifier` (event vs polling)

✔ Required for pluggability

---

### ✅ 2. Multiple implementations exist

Example:

- Postgres / Redis / SQL Server providers

---

### ✅ 3. It enables architectural decoupling

Example:

- Execution pipeline vs storage layer

---

## 2.3 Testing Without Interfaces

Instead of interfaces:

### Prefer:

- Testing concrete classes directly
- Using **real implementations where possible**
- Using **test-specific fakes only when needed**

Example:

```csharp
var store = new InMemoryJobStore();
var service = new JobSubmitter(store, ...);
```

👉 This aligns with your **InMemory provider** design

---

## 2.4 When You DO Mock

Only mock:

- External boundaries (e.g., network, DB if needed)
- Time
- Randomness

---

# 3. Cancellation Token Standards

## 3.1 Always Accept CancellationToken

All async public APIs must include:

```csharp
CancellationToken ct = default
```

Examples:

- `IJobStore`
- `IJobHandler`
- `IJobListener`
- `SubmitAsync`

✔ Already consistent with

---

## 3.2 Always Pass It Down

Never ignore tokens:

```csharp
await _store.EnqueueAsync(job, ct);
```

---

## 3.3 Honor Cancellation

- Check `ct.IsCancellationRequested` in loops
- Pass into all awaited operations

---

## 3.4 DO NOT Create New Tokens Arbitrarily

❌ Avoid:

```csharp
new CancellationTokenSource()
```

✔ Only create when:

- Implementing timeouts
- Internal scoped operations

---

## 3.5 Worker Cancellation Semantics

- Worker shutdown must be **graceful**
- In-flight jobs should:
  - Respect cancellation
  - Transition safely (`Processing → Cancelled` if needed)

---

# 4. Error Handling & Exceptions

## 4.1 Never Swallow Exceptions

❌ Avoid:

```csharp
catch { }
```

---

## 4.2 Use Domain Exceptions

From :

- `InvalidJobStatusTransitionException`
- `UnknownJobTypeException`
- etc.

✔ Always prefer **explicit exception types**

---

## 4.3 Preserve Context

```csharp
throw new JobDeserializationException(key, json, ex);
```

---

## 4.4 Fail Fast on Invalid State

- Invalid transitions must throw immediately
- Never “auto-correct” invalid state

---

# 5. State Machine Integrity (CRITICAL)

From :

> State transitions are guarded and must never be bypassed.

---

## Rules:

- All transitions go through **central validation**
- Never set `Status` directly without validation
- No “shortcut transitions”

---

## Example:

```csharp
AssertTransitionAllowed(current, next);
```

---

# 6. Concurrency & Thread Safety

## 6.1 Assume Everything Is Concurrent

- Multiple workers
- Multiple processes
- Multiple machines

---

## 6.2 No Shared Mutable State Without Protection

- Use `ConcurrentDictionary`, `SemaphoreSlim`, etc.
- Avoid static mutable state

---

## 6.3 Idempotency Where Possible

Handlers should ideally be:

- Safe to retry
- Side-effect aware

---

## 6.4 Never Break Atomic Dequeue

From :

> Dequeue must be atomic — this is non-negotiable

---

# 7. Logging Standards

## 7.1 Structured Logging Only

```csharp
_logger.LogInformation("Processing job {JobId}", jobId);
```

---

## 7.2 Never Log Sensitive Payloads

- Avoid full payload dumps
- Log identifiers only

---

## 7.3 Log at Boundaries

- Job start
- Job completion
- Failures
- Retries
- Dead-letter

---

# 8. Serialization Rules

From :

## 8.1 Only Source-Generated JSON

- No `JsonSerializer.Serialize(object)`
- Always use `JsonTypeInfo<T>`

---

## 8.2 No Runtime Type Resolution

- No `Type.GetType`
- No assembly-qualified names

---

## 8.3 Stable Contract Keys

```csharp
JobType = typeof(TJob).Name;
```

---

# 9. Dependency Injection Rules

## 9.1 Constructor Injection Only

❌ No service locator pattern

---

## 9.2 No Optional Dependencies Unless Explicit

If optional:

```csharp
IJobNotifier? notifier = null
```

---

## 9.3 Keep DI Graph Clean

From :

- No circular dependencies
- Providers depend only on Abstractions

---

# 10. Testing Strategy

## 10.1 Test Layers

From :

### Unit Tests

- Core logic
- Retry logic
- State transitions

---

### Integration Tests

- Provider behavior
- Dequeue correctness
- Concurrency

---

## 10.2 Prefer Real Implementations

- Use `InMemoryJobStore`
- Avoid mocking core components

---

## 10.3 Deterministic Tests

- Control time (inject clock if needed)
- Avoid randomness unless controlled

---

## 10.4 Concurrency Tests Are Mandatory

- Multiple workers
- Race conditions
- Partition ownership

---

# 11. Folder & Naming Conventions

From :

## ✅ Use:

- `Internal/`
- `Execution/`
- `Serialization/`

## ❌ Avoid:

- `Helpers/`
- `Utils/`

---

# 12. Performance Guidelines

## 12.1 Avoid Allocations in Hot Paths

- Dequeue loop
- Dispatcher
- Retry handler

---

## 12.2 Use Async Properly

- No blocking (`.Result`, `.Wait()`)

---

## 12.3 Backoff and Polling Must Be Efficient

From :

- Adaptive polling
- Exponential backoff

---

# 13. Extensibility Rules

## 13.1 Providers Must Be Isolated

- No provider-specific logic in Core
- No leaking provider types

---

## 13.2 Core Must Remain Provider-Agnostic

- Only depend on `IJobStore`, `IJobNotifier`

---

# 14. Code Review Checklist

Before merging:

- [ ] AOT-safe?
- [ ] CancellationToken used correctly?
- [ ] No unnecessary interfaces?
- [ ] State transitions validated?
- [ ] Concurrency safe?
- [ ] Logging structured?
- [ ] Tests added?
- [ ] No reflection/dynamic?

---

# 15. Non-Negotiable Rules Summary

1. **Atomic dequeue must never be broken**
2. **State transitions must always be validated**
3. **No reflection / dynamic (AOT rule)**
4. **No “interfaces for testing” anti-pattern**
5. **CancellationToken must be respected everywhere**

---

## Final Note

These standards are designed to **protect the architecture we’ve already built**:

- Strong invariants (state machine, atomic dequeue)
- AOT-first design
- Pluggable providers
- Predictable execution model

Relaxing these rules will not make the system simpler—it will make it fragile.
