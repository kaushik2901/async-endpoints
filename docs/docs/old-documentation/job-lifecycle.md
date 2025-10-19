---
sidebar_position: 8
---

# Job Lifecycle

## Overview

Understanding the job lifecycle is crucial for effectively using AsyncEndpoints. Jobs go through several states as they're processed, and the framework manages these transitions automatically.

## Job Status States

Jobs progress through the following states:

| Status | Value | Description |
|--------|-------|-------------|
| `Queued` | 100 | Job has been created and is waiting to be processed |
| `Scheduled` | 200 | Job is scheduled for delayed execution (during retries) |
| `InProgress` | 300 | Job is currently being processed by a worker |
| `Completed` | 400 | Job has completed successfully |
| `Failed` | 500 | Job has failed and will not be retried |
| `Canceled` | 600 | Job was explicitly canceled and will not be processed |

## Lifecycle Flow

```
[Queued] → [InProgress] → [Completed]
              ↓           ↑
            [Failed] ←────┘
              ↓
           [Scheduled] (for retries)
              ↓
           [Queued] ←── (if retry is possible)
```

### Detailed State Transitions

1. **Queued**: Initial state when job is created
2. **InProgress**: When a worker claims the job for processing
3. **Completed**: When processing succeeds
4. **Failed**: When processing fails and no more retries are allowed
5. **Scheduled**: When processing fails but retries are still available
6. **Canceled**: When job is explicitly canceled

## Job Creation

When a client makes a request to an async endpoint:

1. Request is received and immediately responded to with `202 Accepted`
2. Framework creates a new `Job` object with `Queued` status
3. Job is stored in the configured job store
4. Background worker will eventually claim this job

## Job Processing

The background worker processes jobs through these steps:

1. **Claim**: Worker claims the next available job from the store
2. **Update Status**: Job status changes to `InProgress`
3. **Execute Handler**: Your registered handler is executed
4. **Complete/Fail**: Based on handler result, job is marked as `Completed` or `Failed`

## Retry Mechanism

When a job fails, a sophisticated retry mechanism is applied:

1. **Check Retry Count**: Compare current retry count with maximum allowed
2. **Increment Counter**: If retries remain, increment retry count
3. **Calculate Delay**: Apply exponential backoff formula: `2^retryCount * baseDelay`
4. **Schedule Retry**: Set job status to `Scheduled` with retry time
5. **Release Worker**: Unassign worker from job
6. **Reset**: When retry time arrives, job returns to `Queued`

### Exponential Backoff

The retry delay follows an exponential backoff pattern:

- 1st retry: `2^1 * baseDelay = 2 * 2s = 4s` delay
- 2nd retry: `2^2 * baseDelay = 4 * 2s = 8s` delay  
- 3rd retry: `2^3 * baseDelay = 8 * 2s = 16s` delay
- And so on...

## Job Status Tracking

### Job Object Properties

The `Job` class contains comprehensive tracking information:

```csharp
public sealed class Job
{
    public Guid Id { get; init; }                    // Unique job identifier
    public string Name { get; set; }                 // Job name for identification
    public JobStatus Status { get; set; }            // Current status
    public string Payload { get; init; }             // Serialized request data
    public string? Result { get; set; }              // Processing result when completed
    public AsyncEndpointError? Error { get; set; }   // Error details when failed
    public int RetryCount { get; set; }              // Number of retry attempts
    public int MaxRetries { get; set; }              // Maximum allowed retries
    public DateTime? RetryDelayUntil { get; set; }   // Time when retry becomes available
    public Guid? WorkerId { get; set; }              // ID of worker processing this job
    public DateTimeOffset CreatedAt { get; set; }    // When job was created
    public DateTimeOffset? StartedAt { get; set; }   // When processing started
    public DateTimeOffset? CompletedAt { get; set; } // When processing completed
    public DateTimeOffset LastUpdatedAt { get; set; } // When job was last updated
    public Dictionary<string, List<string?>> Headers { get; set; } // Original headers
    public Dictionary<string, object?> RouteParams { get; set; }   // Original route params
    public List<KeyValuePair<string, List<string?>>> QueryParams { get; set; } // Original query params
}
```

## Job Status Endpoint

Clients can track job progress using the status endpoint:

```csharp
app.MapAsyncGetJobDetails("/jobs/{jobId:guid}");
```

The response includes all job information:

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "ProcessData",
  "status": "InProgress",
  "retryCount": 0,
  "maxRetries": 3,
  "createdAt": "2025-10-15T10:30:00.000Z",
  "startedAt": "2025-10-15T10:30:15.000Z",
  "completedAt": null,
  "lastUpdatedAt": "2025-10-15T10:30:15.000Z",
  "result": null,
  "error": null,
  "headers": {},
  "routeParams": {},
  "queryParams": {}
}
```

## Custom Status Handling

You can customize how status responses are formatted:

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.ResponseConfigurations.JobStatusResponseFactory = async (jobResult, context) =>
    {
        if (jobResult.IsSuccess && jobResult.Data != null)
        {
            var job = jobResult.Data;
            
            // Customize response based on job status
            if (job.Status == JobStatus.Completed)
            {
                return Results.Ok(new
                {
                    jobId = job.Id,
                    status = job.Status.ToString(),
                    result = job.Result,
                    completedAt = job.CompletedAt
                });
            }
            else if (job.Status == JobStatus.Failed)
            {
                return Results.Problem(new ProblemDetails
                {
                    Title = "Job Failed",
                    Detail = job.Error?.Message,
                    Status = 500
                });
            }
            
            // Return status for other states
            return Results.Ok(new
            {
                jobId = job.Id,
                status = job.Status.ToString(),
                progress = GetStatusProgress(job.Status)
            });
        }
        
        return Results.NotFound("Job not found");
    };
});

private static int GetStatusProgress(JobStatus status)
{
    return status switch
    {
        JobStatus.Queued => 10,
        JobStatus.InProgress => 50,
        JobStatus.Completed => 100,
        _ => 0
    };
}
```

## Distributed Recovery

In multi-instance deployments, jobs may become "stuck" if a worker crashes:

1. **Detection**: Background service monitors for jobs with stale claims
2. **Timeout**: Jobs claimed longer than configured timeout are eligible for recovery
3. **Requeue**: Stuck jobs are returned to the queue for processing
4. **Retry**: If max retries exceeded, job is marked as `Failed`

## Best Practices

### For Job Status
- Monitor job status endpoints for operational visibility
- Implement client-side polling with exponential backoff
- Handle all possible job status states in your client
- Log job state transitions for debugging

### For Retry Logic
- Set appropriate retry counts based on failure patterns
- Configure reasonable base delay for your operations
- Monitor retry frequency and patterns
- Consider circuit breaker patterns for repeated failures

### For Error Handling
- Implement structured error handling in your handlers
- Provide meaningful error messages for debugging
- Log job failures with sufficient context
- Set up alerts for high failure rates

## Example: Tracking Job Progress

```csharp
// Client-side JavaScript example
async function submitJob(data) {
    const response = await fetch('/api/process-data', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data)
    });
    
    const jobInfo = await response.json();
    const jobId = jobInfo.id;
    
    // Poll for job status
    const statusResponse = await pollJobStatus(jobId);
    return statusResponse;
}

async function pollJobStatus(jobId) {
    let status = 'Queued';
    let retryCount = 0;
    const maxRetries = 30; // Maximum poll attempts
    
    while (status !== 'Completed' && status !== 'Failed' && retryCount < maxRetries) {
        await new Promise(resolve => setTimeout(resolve, 2000)); // 2 second delay
        
        const response = await fetch(`/jobs/${jobId}`);
        const jobData = await response.json();
        
        status = jobData.status;
        retryCount++;
        
        console.log(`Job ${jobId} status: ${status}`);
    }
    
    return jobData;
}
```

Understanding the job lifecycle helps you design robust asynchronous operations with proper error handling, monitoring, and user feedback mechanisms.