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

This structure enables **clean NuGet packaging**:

| Project      | NuGet Package                 | Responsibility           |
| ------------ | ----------------------------- | ------------------------ |
| Abstractions | `AsyncEndpoints.Abstractions` | Interfaces + contracts   |
| Core         | `AsyncEndpoints`              | Main orchestration + DI  |
| Worker       | `AsyncEndpoints.Worker`       | BackgroundService engine |
| AspNetCore   | `AsyncEndpoints.AspNetCore`   | Endpoints + minimal API  |
| Providers    | `AsyncEndpoints.Provider.*`   | Storage implementations  |

👉 This matches your pluggable architecture (especially `IJobStore`) perfectly.

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
│   ├── IJobNotifier.cs
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

- **ZERO dependencies**
- No DI, no logging, no EF, nothing
- This is your **public contract surface**

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
│   ├── EventDrivenJobListener.cs
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

- Implements orchestration described in your doc:
  - Listener selection (event vs polling)
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

Matches your design:

> BackgroundService + heartbeat + handler execution

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

From your API design section

---

# 🗄️ 5. Provider Packages (CRITICAL DESIGN)

Each provider is **independent NuGet package**:

Example:

```
AsyncEndpoints.Provider.Postgres/
│
├── Storage/
│   ├── PostgresJobStore.cs
│
├── Notification/
│   ├── PostgresJobNotifier.cs
│
├── Listener/
│   ├── PostgresListenerExtensions.cs
│
└── DependencyInjection/
    ├── PostgresServiceCollectionExtensions.cs
```

Same for:

- SqlServer
- Redis (with special `RedisBlockingJobListener`)
- InMemory

### 🔑 Rule

Each provider:

- Implements `IJobStore`
- Optionally implements `IJobNotifier`
- Registers itself via:

```csharp
options.UsePostgres(...)
```

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

# 💡 Opinionated Improvements (based on your design)

### 1. Separate Worker package is the right call

Because:

- Some users only enqueue jobs (API-only apps)

---

### 2. Redis special case deserves its own listener

Your note about `BLMOVE` is important
👉 Keep it **inside Redis provider**, not Core.

---

### 3. Keep "Progressive API" in Core only

Do NOT leak complexity into providers.

---

# 🧭 Final Take

Architecture is already **top-tier**. The folder structure should:

- Mirror **your abstractions**
- Enable **independent evolution of providers**
- Keep **Core small and orchestration-focused**
- Make NuGet consumption **modular and predictable**

---
