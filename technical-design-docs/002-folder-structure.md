# 🧱 Recommended Solution Structure (NuGet-friendly)

```
AsyncEndpoints.sln
│
├── src/
│   ├── AsyncEndpoints.Abstractions/
│   ├── AsyncEndpoints.Core/
│   ├── AsyncEndpoints.Worker/
│   ├── AsyncEndpoints.AspNetCore/
│   │
│   ├── AsyncEndpoints.Provider.SqlServer/
│   ├── AsyncEndpoints.Provider.Postgres/
│   ├── AsyncEndpoints.Provider.Redis/
│   ├── AsyncEndpoints.Provider.InMemory/
│
├── tests/
│   ├── AsyncEndpoints.UnitTests/
│   ├── AsyncEndpoints.IntegrationTests/
│
└── samples/
    ├── Sample.Api/
    ├── Sample.Worker/
```

---

# 📦 Package Strategy (IMPORTANT)

This structure enables clean NuGet packaging:

| Project      | NuGet Package                 | Responsibility           |
| ------------ | ----------------------------- | ------------------------ |
| Abstractions | `AsyncEndpoints.Abstractions` | Interfaces + contracts   |
| Core         | `AsyncEndpoints`              | Main orchestration + DI  |
| Worker       | `AsyncEndpoints.Worker`       | BackgroundService engine |
| AspNetCore   | `AsyncEndpoints.AspNetCore`   | Endpoints + minimal API  |
| Providers    | `AsyncEndpoints.Provider.*`   | Storage implementations  |

👉 This matches the pluggable `IJobStore` architecture while standardizing on a single polling listener.

---

# 🧩 1. `AsyncEndpoints.Abstractions`

```
AsyncEndpoints.Abstractions/
│
├── Jobs/
│   ├── IJobHandler.cs
│   ├── JobDescriptor.cs
│   ├── JobRecord.cs
│   ├── JobStatus.cs
│
├── Storage/
│   ├── IJobStore.cs
│
├── Listener/
│   ├── IJobListener.cs
│
├── Submission/
│   ├── IJobSubmitter.cs
│
├── Partitioning/
│   ├── IPartitionAssigner.cs
│
└── Common/
    ├── Result.cs
```

### 🔑 Rules

- ZERO dependencies
- No DI, no logging, no EF, nothing
- This is your public contract surface

---

# ⚙️ 2. `AsyncEndpoints.Core`

```
AsyncEndpoints.Core/
│
├── DependencyInjection/
│   ├── ServiceCollectionExtensions.cs
│
├── Configuration/
│   ├── AsyncEndpointsOptions.cs
│   ├── AsyncEndpointsOptionsBuilder.cs
│
├── Submission/
│   ├── JobSubmitter.cs
│
├── Execution/
│   ├── JobExecutor.cs
│   ├── JobDispatcher.cs
│
├── Listener/
│   ├── PollingJobListener.cs
│
├── Partitioning/
│   ├── PartitionManager.cs
│   ├── LeaseBasedPartitionAssigner.cs
│
├── Channels/
│   ├── ChannelManager.cs
│
├── Serialization/
│   ├── JobSerializer.cs
│
└── Internal/
    ├── ServiceResolver.cs
```

### 🔑 Purpose

- Implements orchestration:
  - Single adaptive polling listener over `IJobStore`
  - Submission flow
  - Channel + partition logic

---

# 🔄 3. `AsyncEndpoints.Worker`

```
AsyncEndpoints.Worker/
│
├── Hosting/
│   ├── JobWorkerService.cs
│   ├── WorkerOptions.cs
│
├── Execution/
│   ├── JobExecutionPipeline.cs
│   ├── RetryHandler.cs
│
├── Heartbeat/
│   ├── HeartbeatService.cs
│
└── Concurrency/
    ├── WorkerConcurrencyManager.cs
```

### 🔑 Purpose

BackgroundService + heartbeat + handler execution with adaptive polling.

---

# 🌐 4. `AsyncEndpoints.AspNetCore`

```
AsyncEndpoints.AspNetCore/
│
├── Endpoints/
│   ├── JobEndpoints.cs
│   ├── JobStatusEndpoint.cs
│   ├── JobResultEndpoint.cs
│
├── Extensions/
│   ├── EndpointRouteBuilderExtensions.cs
│
└── Models/
    ├── JobResponse.cs
```

### 🔑 Purpose

Implements:

```
POST → enqueue
GET  → status
```

---

# 🗄️ 5. Provider Packages (CRITICAL DESIGN)

Each provider is an independent NuGet package:

```
AsyncEndpoints.Provider.Postgres/
│
├── Storage/
│   ├── PostgresJobStore.cs
│
└── DependencyInjection/
    ├── PostgresServiceCollectionExtensions.cs
```

Same for:

- SqlServer
- Redis
- InMemory

### 🔑 Rule

Each provider:

- Implements `IJobStore`
- Registers itself via:

```csharp
options.UsePostgres(...)
```

There are no provider notifiers or event-driven listeners; the engine uses a single `PollingJobListener`.

---

# 🧪 6. Tests

```
tests/
│
├── AsyncEndpoints.UnitTests/
│   ├── Core/
│   ├── Worker/
│
├── AsyncEndpoints.IntegrationTests/
│   ├── Postgres/
│   ├── Redis/
│   ├── SqlServer/
```

---

# 🧼 Internal Folder Guidelines (VERY IMPORTANT)

Inside each project:

### ✅ Use:

- `Internal/` → non-public implementation
- `Extensions/` → DI wiring
- `Abstractions/` → only in Abstractions project

### ❌ Avoid:

- `Helpers/`
- `Utils/`
- `Common/` (except in Abstractions)

---

# 📦 NuGet Packaging Best Practices

### Each `.csproj`:

```xml
<PropertyGroup>
  <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
  <PackageId>AsyncEndpoints.Provider.Postgres</PackageId>
  <Version>1.0.0</Version>
  <Authors>YourName</Authors>
  <Description>Postgres provider for AsyncEndpoints</Description>
</PropertyGroup>
```

---

# 🔗 Dependency Graph (Clean)

```
AspNetCore ─┐
            ├── Core ─── Abstractions
Worker ─────┘

Providers ───────────────┘
```

✔ No circular dependencies
✔ Providers depend only on Abstractions (optionally Core for helpers)

---

# 💡 Opinionated Improvements

### 1. Separate Worker package is the right call

- Some users only enqueue jobs (API-only apps)

---

### 2. Standardize on adaptive polling across all providers

- Simplifies mental model and docs; consistent behavior for SQL, Postgres, Redis, and InMemory
- Removes notifier/event complexity and reduces operational risk

---

### 3. Keep "Progressive API" in Core only

- Do not leak complexity into providers

---

# 🧭 Final Take

Architecture remains modular and NuGet-friendly. The folder structure:

- Mirrors abstractions cleanly
- Enables independent evolution of providers
- Keeps Core small and orchestration-focused
- Makes consumption predictable — with a single, polling-based worker model

---
