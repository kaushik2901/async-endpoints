# Agent Prompt: Phase 03 — Configuration Consolidation

## Role
You are implementing Phase 03 of the AsyncEndpoints architecture realignment. You flatten the 6 fragmented configuration classes into a single `AsyncEndpointsOptions` builder pattern.

## Key Architectural Context
Currently there are 6 config classes spread across Core: `AsyncEndpointsConfigurations`, `AsyncEndpointsWorkerConfigurations`, `AsyncEndpointsJobManagerConfigurations`, `AsyncEndpointsObservabilityConfigurations`, `AsyncEndpointsRecoveryConfigurations`, `AsyncEndpointsResponseConfigurations` (moved to AspNetCore in Phase 02).

The target is a single `AsyncEndpointsOptions` with a fluent builder:
```csharp
services.AddAsyncEndpointsCore(options => options.WithMaxConcurrency(4).WithMaxRetries(5));
```

Plus per-channel configuration via `ChannelBuilder`, partition config via `PartitionOptions`, and worker-specific options in `WorkerOptions`.

## Current State Before Phase
- Core is clean of ASP.NET deps (Phase 02 done)
- 5 config classes still exist in Core (ResponseConfigs moved to AspNetCore)
- Numerous references to old config classes throughout the codebase
- No single unified options class

## Phase Goal
Replace all old config classes with clean options models. Delete 5 files in Core. Update all references.

## Task List

### 3.1 Create `Core/Configuration/AsyncEndpointsOptions.cs`
Properties: `MaxConcurrency` (int, default `Environment.ProcessorCount`), `MaxRetries` (3), `HeartbeatInterval` (30s), `StaleJobTimeout` (120s), `PollingMinInterval` (100ms), `PollingMaxInterval` (30s), `DefaultChannel` ("default"), `EnablePartitioning` (false), `ObservabilityEnabled` (true), `SerializerOptions` (JsonSerializerOptions?)

### 3.2 Create `Core/Configuration/AsyncEndpointsOptionsBuilder.cs`
Fluent methods: `WithMaxConcurrency(int)`, `WithMaxRetries(int)`, `WithHeartbeatInterval(TimeSpan)`, `WithStaleJobTimeout(TimeSpan)`, `WithPollingInterval(TimeSpan min, TimeSpan max)`, `WithDefaultChannel(string)`, `EnablePartitioning(bool)`, `WithObservability(bool)`, `WithSerializerOptions(JsonSerializerOptions)`, `Build() → AsyncEndpointsOptions`

### 3.3 Create `Core/Configuration/ChannelBuilder.cs`
Method: `AddChannel(string name, Action<ChannelOptions>? configure = null)` where `ChannelOptions` has `Name`, `MaxConcurrency`, `MaxRetries`

### 3.4 Create `Core/Configuration/PartitionOptions.cs`
Properties: `PartitionCount`, `LeaseTimeout`, `RebalanceInterval`

### 3.5 Create `Worker/Hosting/WorkerOptions.cs`
Properties: `PollingIntervalMin`, `PollingIntervalMax`, `MaxConcurrency`, `HeartbeatInterval`, `StaleJobTimeout`

### 3.6 Delete 5 old config files from Core/Configuration:
- `AsyncEndpointsConfigurations.cs`
- `AsyncEndpointsWorkerConfigurations.cs`
- `AsyncEndpointsJobManagerConfigurations.cs`
- `AsyncEndpointsRecoveryConfigurations.cs`
- `AsyncEndpointsObservabilityConfigurations.cs`

### 3.7 Update all references across the codebase (grep for old class names, replace with new options)

### 3.8 Refactor `AsyncEndpointsResponseConfigurations` in AspNetCore — either fold into `AspNetCoreOptions` or eliminate

### 3.9 Write unit tests for options/builder/channel/partition models

## Validation
- `dotnet build` succeeds for entire solution
- 5 old config files deleted (no grep hits for old class names except in git history)
- `AsyncEndpointsOptionsBuilder` fluent API works end-to-end
- `dotnet test tests/AsyncEndpoints.Core.UnitTests/` passes

## Do NOT
- Modify provider implementations
- Modify Abstractions
- Change the AspNetCore endpoint logic
