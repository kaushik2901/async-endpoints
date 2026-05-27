# Phase 05: Core Orchestration Services

**Goal**: Build new orchestration services in Core: `JobSubmitter`, `PollingJobListener`, `JobDispatcher`, `ChannelManager`, and partitioning support.

**Prerequisites**: Phase 01 (interfaces), Phase 02 (Core cleaned), Phase 03 (options), Phase 04 (IJobStore implementations ready)

---

## Tasks

### 5.1 Create JobSubmitter

- [ ] Create `src/AsyncEndpoints.Core/Submission/JobSubmitter.cs`
- [ ] Implement `IJobSubmitter`
- [ ] Constructor injects: `IJobStore`, `ISerializer`
- [ ] `SubmitAsync<T>`:
  - [ ] Serialize `T job` to JSON string using `ISerializer`
  - [ ] Create `JobDescriptor` with serialized payload, channel, partition key
  - [ ] Call `IJobStore.EnqueueAsync(descriptor, ct)`
  - [ ] Return `JobId`

**Validation**:
```csharp
var submitter = new JobSubmitter(mockStore.Object, mockSerializer.Object);
var id = await submitter.SubmitAsync(new MyPayload { ... });
Assert.NotEqual(Guid.Empty, id);
```

---

### 5.2 Create PollingJobListener

- [ ] Create `src/AsyncEndpoints.Core/Listener/PollingJobListener.cs`
- [ ] Implement `IJobListener`
- [ ] Constructor injects: `IJobStore`, `IOptions<AsyncEndpointsOptions>` (or `WorkerOptions`)
- [ ] Adaptive backoff algorithm:
  - [ ] On dequeue success (got a job): reset interval to `PollingMinInterval`
  - [ ] On dequeue empty: double interval up to `PollingMaxInterval`
  - [ ] Respect `CancellationToken` for graceful shutdown
- [ ] `WaitForNextJobAsync(channel, partitions, ct)`:
  - [ ] Call `IJobStore.DequeueAsync(channel, partitions, ct)`
  - [ ] Apply adaptive backoff logic
  - [ ] Return `JobRecord?`

**Validation**:
```csharp
var listener = new PollingJobListener(mockStore.Object, options);
var job = await listener.WaitForNextJobAsync("default", null, CancellationToken.None);
// Returns null or a job depending on mock setup
```

---

### 5.3 Create JobDispatcher

- [ ] Create `src/AsyncEndpoints.Core/Execution/JobDispatcher.cs`
- [ ] Constructor injects: `IServiceProvider`, `ISerializer`
- [ ] `DispatchAsync(JobRecord record, CancellationToken ct)`:
  - [ ] Deserialize `record.Payload` to `T` using `ISerializer`
  - [ ] Resolve `IJobHandler<T>` from `IServiceProvider`
  - [ ] Invoke `handler.HandleAsync(job, ct)`
  - [ ] Return success or exception info
- [ ] Handle missing handler registration gracefully (log warning, mark job as failed)

**Validation**:
```csharp
var dispatcher = new JobDispatcher(serviceProvider, serializer);
var result = await dispatcher.DispatchAsync(record, ct);
// result indicates success/failure
```

---

### 5.4 Create ChannelManager

- [ ] Create `src/AsyncEndpoints.Core/Channels/ChannelManager.cs`
- [ ] Constructor injects: channel configurations (from `AsyncEndpointsOptions` or `ChannelBuilder`)
- [ ] Methods:
  - [ ] `GetChannelNames() → IReadOnlyList<string>`
  - [ ] `GetChannelConfig(string name) → ChannelConfig?`
  - [ ] `GetConfiguredChannels() → IReadOnlyDictionary<string, ChannelConfig>`
- [ ] `ChannelConfig` record: `Name`, `MaxConcurrency`, `MaxRetries`, `Partitions` (optional set of ints)

**Validation**: `ChannelManager` returns correct config for named channels. Returns null for unknown channels.

---

### 5.5 Create PartitionManager and LeaseBasedPartitionAssigner

- [ ] Create `src/AsyncEndpoints.Core/Partitioning/PartitionManager.cs`
- [ ] Constructor injects: `IPartitionAssigner`, partition config
- [ ] Methods:
  - [ ] `AssignWorkerToPartitionAsync(string workerId, string channel, CancellationToken ct) → int?`
  - [ ] `ReleasePartitionAsync(int partition, string workerId, CancellationToken ct)`
  - [ ] `GetAssignedPartitions(string workerId) → IReadOnlySet<int>`
- [ ] Create `src/AsyncEndpoints.Core/Partitioning/LeaseBasedPartitionAssigner.cs`
  - [ ] Implement `IPartitionAssigner`
  - [ ] Acquire lease via store, renew periodically, release on shutdown

**Validation**: Partition assignment works in a basic scenario with mocked lease store.

---

### 5.6 Create Core DI registration

- [ ] Create/Update `src/AsyncEndpoints.Core/DependencyInjection/ServiceCollectionExtensions.cs`
- [ ] Define `AddAsyncEndpointsCore(this IServiceCollection services, Action<AsyncEndpointsOptionsBuilder>? configure = null)`
- [ ] Register:
  - [ ] `IJobSubmitter` → `JobSubmitter` (scoped or singleton)
  - [ ] `IJobListener` → `PollingJobListener` (singleton)
  - [ ] `JobDispatcher` (singleton — thread-safe)
  - [ ] `ChannelManager` (singleton)
  - [ ] `ISerializer` → `JobSerializer` (singleton)
  - [ ] `AsyncEndpointsOptions` via options pattern
  - [ ] If partitioning enabled: `IPartitionAssigner` → `LeaseBasedPartitionAssigner`, `PartitionManager`

**Validation**: `services.AddAsyncEndpointsCore()` registers all Core services. Can resolve `IJobSubmitter` from DI.

---

### 5.7 Write unit tests

- [ ] In `tests/AsyncEndpoints.Core.UnitTests/`:
  - [ ] `JobSubmitterTests`:
    - [ ] `SubmitAsync_SerializesJob_AndCallsStore`
    - [ ] `SubmitAsync_ReturnsJobId_FromStore`
    - [ ] `SubmitAsync_UsesDefaultChannel_WhenNoneSpecified`
  - [ ] `PollingJobListenerTests`:
    - [ ] `WaitForNextJobAsync_CallsStoreDequeue`
    - [ ] `WaitForNextJobAsync_AdaptiveBackoff_ResetsOnSuccess`
    - [ ] `WaitForNextJobAsync_AdaptiveBackoff_DoublesOnEmpty`
    - [ ] `WaitForNextJobAsync_RespectsCancellationToken`
  - [ ] `JobDispatcherTests`:
    - [ ] `DispatchAsync_ResolvesHandler_AndInvokesIt`
    - [ ] `DispatchAsync_DeserializesPayload_ToCorrectType`
    - [ ] `DispatchAsync_ReturnsFailure_WhenHandlerNotFound`
    - [ ] `DispatchAsync_ReturnsFailure_WhenHandlerThrows`
  - [ ] `ChannelManagerTests`:
    - [ ] `GetChannelNames_ReturnsConfiguredChannels`
    - [ ] `GetChannelConfig_ReturnsConfig_ForKnownChannel`
    - [ ] `GetChannelConfig_ReturnsNull_ForUnknownChannel`
  - [ ] `PartitionManagerTests`:
    - [ ] `AssignWorkerToPartition_ReturnsPartition`
    - [ ] `GetAssignedPartitions_ReturnsCorrectSet`

**Validation**: `dotnet test tests/AsyncEndpoints.Core.UnitTests/` passes with good coverage.

---

## Phase 05 Definition of Done

- [ ] `JobSubmitter` implements `IJobSubmitter` — serializes payload, calls `IJobStore.EnqueueAsync`
- [ ] `PollingJobListener` implements `IJobListener` — adaptive backoff, delegates to `IJobStore.DequeueAsync`
- [ ] `JobDispatcher` resolves `IJobHandler<T>` from DI, deserializes payload, invokes handler
- [ ] `ChannelManager` manages named channel configurations
- [ ] `PartitionManager` + `LeaseBasedPartitionAssigner` handle partition leasing
- [ ] `ServiceCollectionExtensions.AddAsyncEndpointsCore()` registers all services
- [ ] `dotnet build` succeeds
- [ ] Unit tests pass for all new services

**Next phase**: [Phase 06: Worker Engine Redesign](phase-06-worker-redesign.md)
