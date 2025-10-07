# Redis Lua Script Service Design

## Overview
This document outlines the design for implementing a dedicated service that handles Redis operations using Lua scripts. The goal is to extract Lua script execution logic from the `RedisJobStore` class into a separate, testable service.

## Current State
Currently, the `RedisJobStore` class contains Lua script execution logic inline within its methods:
- `ClaimSingleJob` method contains a Lua script for atomically claiming jobs
- `RecoverStuckJobs` method contains a Lua script for recovering stuck jobs

## Proposed Solution
Create a dedicated service called `RedisLuaScriptService` that will:
1. Contain all Lua script execution logic
2. Accept `IDatabase` and required parameters as inputs
3. Return appropriate results for the calling methods

## Service Interface
```csharp
public interface IRedisLuaScriptService
{
    Task<MethodResult<RedisValue[]>> ClaimSingleJob(
        IDatabase database, 
        Guid jobId, 
        Guid workerId, 
        IDateTimeProvider dateTimeProvider, 
        CancellationToken cancellationToken = default);
    
    Task<int> RecoverStuckJobs(
        IDatabase database, 
        long timeoutUnixTime, 
        int maxRetries, 
        double retryDelayBaseSeconds, 
        IDateTimeProvider dateTimeProvider, 
        CancellationToken cancellationToken = default);
}
```

## Implementation Details

### ClaimSingleJob Method
- Executes a Lua script that atomically checks and claims a job
- Ensures job status is appropriate (Queued or Scheduled)
- Checks for retry delay if applicable
- Updates job status to InProgress and assigns to worker
- Removes job from queue
- Returns RedisValue array containing the raw job data
- The calling service (RedisJobStore) will be responsible for converting these values to a Job object using existing deserialization logic

### RecoverStuckJobs Method
- Executes a Lua script that finds stuck jobs (status = InProgress for longer than timeout)
- For jobs with retries remaining: updates to Scheduled with exponential backoff delay
- For jobs exceeding max retries: marks as Failed
- Updates worker assignment and timestamps appropriately

## Benefits
1. **Separation of Concerns**: Lua script logic is separated from general job store operations
2. **Testability**: Lua script execution can be tested in isolation with integration tests
3. **Maintainability**: Lua scripts are centralized in one location
4. **Reusability**: The service can be used by other classes if needed

## Integration with RedisJobStore
- `RedisJobStore` will depend on `IRedisLuaScriptService`
- The current `ClaimSingleJob` and `RecoverStuckJobs` methods will delegate to the service
- This maintains the same public API while improving internal architecture

## Future Considerations
- Integration tests can be written to verify Lua script behavior with actual Redis instances
- Additional Lua script operations can be added to the service as needed
- Performance monitoring of Lua script execution can be centralized