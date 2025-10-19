# Preserving Job Execution Data Across Retries in AsyncEndpoints

## Overview
This document outlines the approach for preserving job execution data that gets overridden during retries in the AsyncEndpoints system. Currently, when a job is retried, certain fields in the `Job` entity are overwritten, losing historical execution information. This document details how to preserve this data while maintaining the existing job lifecycle.

## Current State Analysis
The existing `Job` class contains fields that get overridden during retries:
- `StartedAt` - Timestamp of when the job started processing (overwritten on each retry)
- `CompletedAt` - Timestamp of when the job finished processing (overwritten on each retry)
- `Result` - The result of the job execution (overwritten on each retry/failure)
- `Error` - Error details if the job failed (overwritten on each retry/failure)
- `WorkerId` - The ID of the worker processing the job (overwritten on each retry)
- `RetryDelayUntil` - The time until which the job is scheduled for retry (overwritten on each retry)

## Fields That Need Preservation

### 1. Execution History
- **Previous Starts/Completions**: Historical timestamps of all job execution attempts
- **Previous Results/Errors**: All previous execution results and errors for debugging
- **Previous Workers**: Which workers handled previous attempts

### 2. Retry Information
- **Retry Count History**: How many times the job was retried and when
- **Retry Reasons**: Why each retry was initiated (error type, conditions, etc.)
- **Retry Delay History**: All scheduled delay times across retries

### 3. Performance Data
- **Execution Duration History**: How long each execution attempt took
- **Performance Patterns**: Identify if job performance changes across retries

## Proposed Implementation

### Option 1: Enhanced Job Class
Extend the existing `Job` class to maintain historical data:

```csharp
public class Job(DateTimeOffset currentTime)
{
    // ... existing properties ...

    // Collection to store historical execution data
    public List<JobExecutionHistory> ExecutionHistory { get; set; } = new();

    // New method to record execution data before it gets overwritten
    public void RecordExecutionAttempt()
    {
        var currentAttempt = new JobExecutionHistory
        {
            AttemptNumber = this.RetryCount + 1, // Current attempt
            StartedAt = this.StartedAt,
            CompletedAt = this.CompletedAt,
            Result = this.Result,
            Error = this.Error,
            WorkerId = this.WorkerId,
            Status = this.Status
        };
        
        this.ExecutionHistory.Add(currentAttempt);
    }
}

public class JobExecutionHistory
{
    public int AttemptNumber { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Result { get; set; }
    public AsyncEndpointError? Error { get; set; }
    public Guid? WorkerId { get; set; }
    public JobStatus Status { get; set; }
    public long DurationMs { get; set; } // Calculated from StartedAt/CompletedAt
}
```

### Option 2: Separate Storage Entity
Maintain a separate collection for historical execution data:

```csharp
public class JobExecutionRecord
{
    public Guid JobId { get; set; }
    public int AttemptNumber { get; set; } // 1, 2, 3, etc.
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public long DurationMs { get; set; }
    public Guid? WorkerId { get; set; }
    public string? Result { get; set; }
    public AsyncEndpointError? Error { get; set; }
    public JobStatus Status { get; set; }
    public string? ErrorMessage => Error?.Message;
    public string? ErrorCode => Error?.Code;
}

public interface IJobExecutionHistoryStore
{
    Task<MethodResult> SaveExecutionRecordAsync(JobExecutionRecord record, CancellationToken cancellationToken);
    Task<MethodResult<List<JobExecutionRecord>>> GetExecutionHistoryAsync(Guid jobId, CancellationToken cancellationToken);
}
```

## Implementation Approach

### 1. Before Overwriting Current Data
In the `JobManager` or `JobProcessorService`, before updating the current job's execution fields, preserve the current values in the historical data structure:

```csharp
// Before updating job status and timestamps in ProcessJobSuccess/ProcessJobFailure
if (job.StartedAt.HasValue && (job.Status == JobStatus.InProgress))
{
    // Record the current execution before it gets overwritten
    var executionRecord = new JobExecutionRecord
    {
        JobId = job.Id,
        AttemptNumber = job.RetryCount, // The attempt that just completed
        StartedAt = job.StartedAt,
        CompletedAt = DateTimeOffset.UtcNow, // Current completion time
        DurationMs = (long)(DateTimeOffset.UtcNow - job.StartedAt.Value).TotalMilliseconds,
        Result = job.Result,
        Error = job.Error,
        Status = job.Status,
        WorkerId = job.WorkerId
    };
    
    await _jobExecutionHistoryStore.SaveExecutionRecordAsync(executionRecord, cancellationToken);
}
```

### 2. Update JobProcessorService
Modify the `JobProcessorService` to record execution data before status updates:

- Before setting job result: Record current execution data
- Before setting job error: Record current execution data
- Maintain the existing flow for current job state

### 3. Update JobManager
Update the `JobManager.ProcessJobSuccess` and `JobManager.ProcessJobFailure` methods to preserve execution history before updating the job state.

## Benefits

### 1. Complete Execution History
- Track all retry attempts with timestamps and results
- Identify patterns in job failures and successes
- Debug failing retries by examining previous attempts

### 2. Improved Debugging
- See the exact error from each retry attempt
- Understand how job behavior changes across retries
- Identify if failures are consistent or variable

### 3. Performance Analysis
- Compare execution times across retries
- Determine if retries are faster/slower than initial attempts
- Identify performance degradation patterns

### 4. Compliance and Audit
- Complete audit trail of job execution attempts
- Required for business processes with compliance needs
- Historical data for performance reporting

## Storage Considerations

### In-Memory Implementation
- Store execution records in a dictionary keyed by JobId
- Implement automatic cleanup for old records to prevent memory growth
- Suitable for development and small-scale deployments

### Redis Implementation
- Use Redis streams or sorted sets to store execution history
- Implement TTL policies to automatically expire old records
- Support for distributed systems and horizontal scaling

## Data Retention Policy

### Automatic Cleanup
To prevent unbounded growth, implement a retention policy:
- Keep last N execution records per job (e.g., 10-50)
- Or keep records for a specific time period (e.g., last 30 days)
- Configurable retention settings per deployment

## Migration Path

### From Current State
- Existing jobs will have no historical data initially
- New executions will start building history
- Optionally create initial history record for existing jobs in progress

### Backward Compatibility
- The change maintains all existing APIs and interfaces
- Historical data is additive and optional
- Existing code continues to work unchanged

## Conclusion
This approach preserves critical job execution data that is currently lost during retries while maintaining the existing job lifecycle. It provides complete visibility into all job execution attempts, enables better debugging and performance analysis, and satisfies compliance requirements for business processes.

The implementation should be straightforward and maintain full backward compatibility while adding valuable historical tracking capabilities.