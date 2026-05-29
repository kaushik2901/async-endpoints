# AsyncEndpoints — Agent Guide

A .NET library for building async APIs with background job processing. Multi-targets `net8.0;net9.0;net10.0`.

## Project Structure

```
src/
├── AsyncEndpoints.Abstractions/   # Interfaces & models (zero deps)
├── AsyncEndpoints.Core/           # DI, config, channels, dispatch, serialization, in-memory store
├── AsyncEndpoints.AspNetCore/     # Minimal API endpoint mapping, HTTP models, response config
├── AsyncEndpoints.Worker/         # BackgroundService, concurrency, heartbeat, retry, stale sweeper
└── AsyncEndpoints.Provider.Redis/ # Redis IJobStore using StackExchange.Redis + Lua scripts
tests/
├── AsyncEndpoints.Abstractions.UnitTests/
├── AsyncEndpoints.Core.UnitTests/
├── AsyncEndpoints.AspNetCore.UnitTests/
├── AsyncEndpoints.Worker.UnitTests/
└── AsyncEndpoints.Provider.Redis.UnitTests/
```

### Layer Dependencies

```
Abstractions ← Core ← AspNetCore
                Core ← Worker
           Abstractions ← Provider.Redis
```

No project depends on Legacy. Legacy namespace (`AsyncEndpoints.Core.Legacy`) is `[Obsolete]` — do not add new code there.

## Build & Test

```powershell
# Build entire solution (all TFMs via sln means multi-target builds)
dotnet build AsyncEndpoints.sln

# Run all tests
dotnet test AsyncEndpoints.sln

# Run a single test project
dotnet test tests/AsyncEndpoints.Core.UnitTests

# Run a single test class
dotnet test tests/AsyncEndpoints.Core.UnitTests --filter "FullyQualifiedName~JobSubmitterTests"

# CI builds + tests only net9.0 (`.github/workflows/build.yml`)
dotnet build AsyncEndpoints.sln -p:TargetFramework=net9.0
dotnet test AsyncEndpoints.sln -p:TargetFramework=net9.0 --collect "XPlat Code Coverage;Format=opencover"

# CI runs on net10.0:
dotnet test AsyncEndpoints.sln --configuration Release --no-build -p:TargetFramework=net10.0
```

No `Directory.Build.props` — each `.csproj` self-describes settings.

## Key Architectural Patterns

- **Producer-Consumer**: HTTP endpoints submit via `IJobSubmitter` → `IJobStore.EnqueueAsync()`, workers consume via `IJobListener` → `IJobStore.DequeueAsync()`
- **Storage abstraction**: `IJobStore` with `InMemoryJobStore` (dev) and `RedisJobStore` (production). Contract tests via `JobStoreContractTestsBase`
- **Pipeline pattern**: `JobExecutionPipeline` coordinates concurrency → heartbeat → dispatch → retry → status update
- **Options + Builder**: `AsyncEndpointsOptionsBuilder` fluent API, separate options per layer
- **Result monad**: `Result<T>` and `MethodResult<T>` with `AsyncEndpointError` for structured error handling
- **AOT compatibility**: `<IsAotCompatible>true</IsAotCompatible>` on all production projects, source-generated `JsonSerializerContext` types, `ISerializer` with both `JsonTypeInfo<T>` and reflection overloads

## Conventions

- **Tabs for .cs**, spaces for .csproj/.json (`.editorconfig`)
- **Namespaces**: Match folder structure, use file-scoped namespaces
- **Nullable**: Enabled everywhere
- **ImplicitUsings**: Enabled
- **Response customization**: `AsyncEndpointsResponseConfigurations` replaces default response factories — don't hardcode response shapes
- **Worker concurrency**: `WorkerConcurrencyManager` uses `SemaphoreSlim` per channel/partition — respect this in worker-scoped changes

## Testing

- **Stack**: xUnit + AutoFixture + Moq with `AutoMoqDataAttribute` for auto-data theories
- **Contract tests**: `JobStoreContractTestsBase` in `AsyncEndpoints.Abstractions.UnitTests` — store implementations inherit it to verify the full contract
- Provider.Redis tests require a real Redis instance (not run in CI)

## Documentation Site

`docs/` is a Docusaurus site (`yarn start`, `yarn build`) — separate from the .NET code. Don't touch unless explicitly asked.

