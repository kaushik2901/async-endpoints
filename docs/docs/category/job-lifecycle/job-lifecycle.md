---
sidebar_position: 4
title: Job Lifecycle
---

# Job Lifecycle

This page details the complete lifecycle of jobs in AsyncEndpoints, including status transitions, retry mechanics, timeout handling, and recovery mechanisms.

## Job Status Progression

Jobs in AsyncEndpoints progress through a series of well-defined states. The following diagram shows the possible state transitions:

```
           ┌─────────────────┐
           │                 │
           │    Queued       │◀─┐
           │                 │  │
           └─────────┬───────┘  │
                     │          │
        ┌────────────┼──────────┼────────────┐
        │            ▼          │            │
        │    ┌─────────────────┐│            │
        │    │                 ││            │
        │    │   Scheduled     ││            │
        │    │                 ││            │
        │    └─────────┬───────┘│            │
        │              │        │            │
        │              ▼        │            │
        │    ┌─────────────────┐│            │
        │    │                 ││            │
        └────┤   InProgress    │├────────────┘
             │                 │
             └─────────┬───────┘
                       │
        ┌──────────────┼──────────────┐
        │              │              │
        ▼              ▼              ▼
    ┌─────────────┐┌─────────────┐┌─────────────┐
    │             ││             ││             │
    │  Completed  ││   Failed    ││   Canceled  │
    │             ││             ││             │
    └─────────────┘└─────────────┘└─────────────┘
```

### Queued Status
- **Description**: Job created and waiting for processing
- **Entry Points**: Initial job creation
- **Transitions**: Can transition to InProgress, Scheduled, Canceled, or Completed
- **Duration**: From job creation until processing begins or timeout
- **Characteristics**: Job is available for workers to claim

### Scheduled Status
- **Description**: Job scheduled for delayed execution, typically due to retry backoff
- **Entry Points**: From Failed status when retries are available
- **Transitions**: Can transition to Queued (when delay expires) or Canceled
- **Duration**: Until the scheduled time arrives
- **Characteristics**: Job will become available again after the delay period

### InProgress Status
- **Description**: Job is currently being processed by a worker
- **Entry Points**: From Queued (or Scheduled) status when claimed by a worker
- **Transitions**: Can transition to Completed, Failed, Scheduled (for retries), or Canceled
- **Duration**: From when processing starts until completion/failure
- **Characteristics**: Job is assigned to a specific worker instance

### Completed Status
- **Description**: Job successfully completed with a result
- **Entry Points**: From InProgress status after successful processing
- **Transitions**: Can transition to Canceled (though uncommon)
- **Characteristics**: Result data is available for retrieval

### Failed Status
- **Description**: Job failed after all retry attempts exhausted or unrecoverable error
- **Entry Points**: From InProgress status after processing failure
- **Transitions**: Can transition to Scheduled (for retries if available), Queued (for immediate retries), or Canceled
- **Characteristics**: Error details are preserved for debugging

### Canceled Status
- **Description**: Job was explicitly canceled before completion
- **Entry Points**: From any status when cancellation is requested
- **Transitions**: Usually terminal, though could potentially be reset
- **Characteristics**: No further processing occurs

## State Transition Validation

The system validates all state transitions to ensure data consistency:

```csharp
private static bool IsValidStateTransition(JobStatus from, JobStatus to)
{
    return (from, to) switch
    {
        // Allow jobs to transition directly to completed/failed from queued
        (JobStatus.Queued, JobStatus.Completed) => true,
        (JobStatus.Queued, JobStatus.Failed) => true,
        (JobStatus.Queued, JobStatus.Canceled) => true,
        (JobStatus.Queued, JobStatus.InProgress) => true,
        (JobStatus.Queued, JobStatus.Scheduled) => true, // For retries with delay
        (JobStatus.Scheduled, JobStatus.Queued) => true, // When scheduled job becomes available
        (JobStatus.Scheduled, JobStatus.Canceled) => true,
        (JobStatus.InProgress, JobStatus.Completed) => true,
        (JobStatus.InProgress, JobStatus.Failed) => true,
        (JobStatus.InProgress, JobStatus.Canceled) => true,
        (JobStatus.InProgress, JobStatus.Scheduled) => true,
        (JobStatus.Failed, JobStatus.Queued) => true, // For retries without delay
        (JobStatus.Failed, JobStatus.Scheduled) => true, // For retries with delay
        (JobStatus.Failed, JobStatus.Canceled) => true,
        (JobStatus.Completed, JobStatus.Canceled) => true, // Completed jobs can be canceled if needed
        _ => from == to // Allow same state updates for timestamp refreshes
    };
}
```

## Retry Mechanics

### Exponential Backoff Algorithm
When jobs fail, AsyncEndpoints implements exponential backoff for retries:

```csharp
private TimeSpan CalculateRetryDelay(int retryCount)
{
    // Exponential backoff: (2 ^ retryCount) * base delay
    return TimeSpan.FromSeconds(Math.Pow(2, retryCount) * _jobManagerConfiguration.RetryDelayBaseSeconds);
}
```

For example, with the default 2-second base delay:
- Retry 0: 2 seconds (2^0 * 2)
- Retry 1: 4 seconds (2^1 * 2) 
- Retry 2: 8 seconds (2^2 * 2)
- Retry 3: 16 seconds (2^3 * 2)
- And so on...

### Retry Process Flow
1. Job execution fails in `InProgress` state
2. Check if `retryCount < maxRetries`
3. If retries available:
   - Increment `retryCount`
   - Calculate next retry time using exponential backoff
   - Set `RetryDelayUntil` to calculated time
   - Update status to `Scheduled`
   - Release worker assignment
4. If no retries available:
   - Set error details
   - Update status to `Failed`

## Timeout Handling

### Job Execution Timeout
Jobs can be configured with execution timeouts to prevent indefinite execution:

```csharp
// Configuration in Program.cs
builder.Services.AddAsyncEndpoints(options =>
{
    options.WorkerConfigurations.JobTimeoutMinutes = 30; // Default: 30 minutes
});
```

When a job exceeds its timeout:
- The job processing is cancelled
- The job transitions to `Failed` status
- An appropriate timeout error is recorded

### Job Claim Timeout
Workers claim jobs from the queue with a configurable timeout:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.JobManagerConfiguration.JobClaimTimeout = TimeSpan.FromMinutes(5);
});
```

## Job Recovery Mechanisms

### Distributed Job Recovery
In multi-instance deployments, AsyncEndpoints can automatically recover stuck jobs:

```csharp
builder.Services.AddAsyncEndpointsWorker(recoveryConfiguration =>
{
    recoveryConfiguration.EnableDistributedJobRecovery = true; // Default: true
    recoveryConfiguration.JobTimeoutMinutes = 30; // Default: 30 minutes
    recoveryConfiguration.RecoveryCheckIntervalSeconds = 300; // Default: 5 minutes
    recoveryConfiguration.MaximumRetries = 3; // Default: 3 retries
});
```

### Recovery Process
1. Periodic checks identify jobs in `InProgress` status that exceed their timeout
2. If a job appears stuck for too long, it's considered failed
3. Retry logic applies to recoverable jobs
4. Workers can claim jobs that were being processed by failed instances

## Job Data Structure

Each job contains comprehensive tracking information:

```csharp
public sealed class Job(DateTimeOffset currentTime)
{
    public Guid Id { get; init; }
    public string Name { get; set; }
    public JobStatus Status { get; set; }
    public Dictionary<string, List<string?>> Headers { get; set; }
    public Dictionary<string, object?> RouteParams { get; set; }
    public List<KeyValuePair<string, List<string?>>> QueryParams { get; set; }
    public string Payload { get; init; }
    public string? Result { get; set; }
    public AsyncEndpointError? Error { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; }
    public DateTime? RetryDelayUntil { get; set; }
    public Guid? WorkerId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; }
}
```

## Concurrency Management

### Per-Worker Concurrency
Each worker instance can be configured with maximum concurrency:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount;
});
```

### Queue Concurrency
- Multiple workers can simultaneously pull jobs from the queue
- Job claims are atomic to prevent duplicate processing
- Semaphore limits control concurrent execution

## Timestamp Management

Jobs maintain several important timestamps:
- **CreatedAt**: When the job was initially created
- **StartedAt**: When processing began (set when status becomes InProgress)
- **CompletedAt**: When processing finished (set when status becomes Completed/Failed/Canceled)
- **LastUpdatedAt**: When the job record was last modified

These timestamps provide full audit trail and metrics for job processing.

## Monitoring Job Lifecycle

### Status Checking
Clients can check job status using the job details endpoint:

```csharp
app.MapAsyncGetJobDetails("/jobs/{jobId:guid}");
```

### Lifecycle Events
The system generates detailed information for each lifecycle stage:
- Job creation with initial status
- Status transitions with timestamps
- Error details when failures occur
- Success results when completed

Understanding the job lifecycle is crucial for designing robust async processing workflows and troubleshooting potential issues.