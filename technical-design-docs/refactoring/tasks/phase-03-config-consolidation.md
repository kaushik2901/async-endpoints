# Phase 03: Configuration Consolidation

**Goal**: Flatten 6 configuration classes into the `AsyncEndpointsOptions` builder pattern.

**Prerequisites**: Phase 01 (needs `Abstractions`), Phase 02 (needs `Core` cleaned)

---

## Tasks

### 3.1 Create AsyncEndpointsOptions in Core

- [ ] Create `src/AsyncEndpoints.Core/Configuration/AsyncEndpointsOptions.cs`
- [ ] Properties:
  - `MaxConcurrency` (int, default `Environment.ProcessorCount`)
  - `MaxRetries` (int, default `3`)
  - `HeartbeatInterval` (TimeSpan, default `30s`)
  - `StaleJobTimeout` (TimeSpan, default `120s`)
  - `PollingMinInterval` (TimeSpan, default `100ms`)
  - `PollingMaxInterval` (TimeSpan, default `30s`)
  - `DefaultChannel` (string, default `"default"`)
  - `EnablePartitioning` (bool, default `false`)
  - `ObservabilityEnabled` (bool, default `true`)
  - `SerializerOptions` (JsonSerializerOptions?)

**Validation**: `AsyncEndpointsOptions` compiles. Can be instantiated with defaults.

---

### 3.2 Create AsyncEndpointsOptionsBuilder

- [ ] Create `src/AsyncEndpoints.Core/Configuration/AsyncEndpointsOptionsBuilder.cs`
- [ ] Fluent methods:
  - `WithMaxConcurrency(int)`
  - `WithMaxRetries(int)`
  - `WithHeartbeatInterval(TimeSpan)`
  - `WithStaleJobTimeout(TimeSpan)`
  - `WithPollingInterval(TimeSpan min, TimeSpan max)`
  - `WithDefaultChannel(string)`
  - `EnablePartitioning(bool)`
  - `WithObservability(bool)`
  - `WithSerializerOptions(JsonSerializerOptions)`
  - `Build() → AsyncEndpointsOptions`
- [ ] Support `IServiceCollection` extension: `AddAsyncEndpointsCore(Action<AsyncEndpointsOptionsBuilder> configure)`

**Validation**: Builder pattern works: `new AsyncEndpointsOptionsBuilder().WithMaxConcurrency(4).Build()`.

---

### 3.3 Create ChannelBuilder

- [ ] Create `src/AsyncEndpoints.Core/Configuration/ChannelBuilder.cs`
- [ ] Properties per channel: `Name`, `MaxConcurrency`, `MaxRetries`
- [ ] Methods: `AddChannel(string name, Action<ChannelOptions>? configure = null)`

**Validation**: `ChannelBuilder` compiles and can configure named channels.

---

### 3.4 Create PartitionOptions

- [ ] Create `src/AsyncEndpoints.Core/Configuration/PartitionOptions.cs`
- [ ] Properties: `PartitionCount` (int), `LeaseTimeout` (TimeSpan), `RebalanceInterval` (TimeSpan)

**Validation**: `PartitionOptions` compiles.

---

### 3.5 Create WorkerOptions in Worker project

- [ ] Create `src/AsyncEndpoints.Worker/Hosting/WorkerOptions.cs`
- [ ] Properties: `PollingIntervalMin`, `PollingIntervalMax`, `MaxConcurrency`, `HeartbeatInterval`, `StaleJobTimeout`

**Validation**: `WorkerOptions` compiles.

---

### 3.6 Delete old configuration classes

- [ ] Delete `src/AsyncEndpoints.Core/Configuration/AsyncEndpointsConfigurations.cs`
- [ ] Delete `src/AsyncEndpoints.Core/Configuration/AsyncEndpointsWorkerConfigurations.cs`
- [ ] Delete `src/AsyncEndpoints.Core/Configuration/AsyncEndpointsJobManagerConfigurations.cs`
- [ ] Delete `src/AsyncEndpoints.Core/Configuration/AsyncEndpointsRecoveryConfigurations.cs`
- [ ] Delete `src/AsyncEndpoints.Core/Configuration/AsyncEndpointsObservabilityConfigurations.cs`

**Note**: `AsyncEndpointsResponseConfigurations.cs` was already moved to `AspNetCore` in Phase 02 — it will be refactored separately in Phase 07.

**Validation**: All old config classes are gone. No compilation errors referencing them.

---

### 3.7 Update all references to use new options

- [ ] Search codebase for references to old config classes (e.g., `AsyncEndpointsConfigurations`, `AsyncEndpointsWorkerConfigurations`)
- [ ] Update each reference to use the new `AsyncEndpointsOptions` pattern
- [ ] Pay special attention to:
  - [ ] `Serializer.cs` — should accept `AsyncEndpointsOptions` or `JsonSerializerOptions`
  - [ ] `AsyncEndpointsObservability` — should read from `AsyncEndpointsOptions`
  - [ ] Any internal service that consumed old configs

**Validation**: All references updated. `dotnet build` succeeds.

---

### 3.8 Refactor AsyncEndpointsResponseConfigurations (in AspNetCore)

- [ ] Move config values from `AsyncEndpointsResponseConfigurations` into `AspNetCoreOptions` (create if needed)
- [ ] Or eliminate entirely — use standard ASP.NET patterns in endpoint code (preferred)

**Validation**: Response config values are accessible via the new options pattern.

---

### 3.9 Write unit tests for new options

- [ ] In `tests/AsyncEndpoints.Core.UnitTests/`:
  - [ ] `AsyncEndpointsOptions` has correct defaults
  - [ ] `AsyncEndpointsOptionsBuilder` fluent API works
  - [ ] `AsyncEndpointsOptionsBuilder.Build()` produces expected values
  - [ ] `ChannelBuilder` creates channel configs correctly
  - [ ] `PartitionOptions` serializes correctly
  - [ ] `WorkerOptions` has correct defaults

**Validation**: `dotnet test tests/AsyncEndpoints.Core.UnitTests/` passes.

---

## Phase 03 Definition of Done

- [ ] 6 old configuration classes deleted
- [ ] `AsyncEndpointsOptions` + builder exists in `Core/Configuration/`
- [ ] `ChannelBuilder` exists in `Core/Configuration/`
- [ ] `PartitionOptions` exists in `Core/Configuration/`
- [ ] `WorkerOptions` exists in `Worker/Hosting/`
- [ ] All old references updated to new pattern
- [ ] `dotnet build` succeeds
- [ ] Unit tests for all new options types pass

**Next phase**: [Phase 04: IJobStore Implementation Migration](phase-04-jobstore-migration.md)
