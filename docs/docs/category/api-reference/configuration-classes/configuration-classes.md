---
sidebar_position: 2
title: Configuration Classes
---

# Configuration Classes

This page provides detailed reference documentation for all AsyncEndpoints configuration classes, including their properties, methods, and usage examples.

## AsyncEndpointsConfigurations

### Class Definition
```csharp
public sealed class AsyncEndpointsConfigurations
```

### Properties

#### WorkerConfigurations
- **Type**: `AsyncEndpointsWorkerConfigurations`
- **Description**: Gets or sets the worker-specific configurations
- **Default**: `new AsyncEndpointsWorkerConfigurations()`

#### JobManagerConfiguration
- **Type**: `AsyncEndpointsJobManagerConfiguration`
- **Description**: Gets or sets the job-manager-specific configurations
- **Default**: `new AsyncEndpointsJobManagerConfiguration()`

#### ResponseConfigurations
- **Type**: `AsyncEndpointsResponseConfigurations`
- **Description**: Gets or sets the response-specific configurations
- **Default**: `new AsyncEndpointsResponseConfigurations()`

### Example
```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.WorkerConfigurations.MaximumConcurrency = Environment.ProcessorCount;
    options.JobManagerConfiguration.DefaultMaxRetries = 5;
    options.ResponseConfigurations.JobSubmittedResponseFactory = async (job, context) =>
    {
        return Results.Accepted($"/jobs/{job.Id}", job);
    };
});
```

---

## AsyncEndpointsWorkerConfigurations

### Class Definition
```csharp
public sealed class AsyncEndpointsWorkerConfigurations
```

### Properties

#### WorkerId
- **Type**: `Guid`
- **Description**: Gets or sets the unique identifier for the worker instance
- **Default**: `Guid.NewGuid()`

#### MaximumConcurrency
- **Type**: `int`
- **Description**: Gets or sets the maximum number of concurrent jobs the worker can process
- **Default**: `Environment.ProcessorCount`

#### PollingIntervalMs
- **Type**: `int`
- **Description**: Gets or sets the polling interval in milliseconds for checking new jobs
- **Default**: `AsyncEndpointsConstants.DefaultPollingIntervalMs`

#### JobTimeoutMinutes
- **Type**: `int`
- **Description**: Gets or sets the timeout in minutes for job execution
- **Default**: `AsyncEndpointsConstants.DefaultJobTimeoutMinutes`

#### BatchSize
- **Type**: `int`
- **Description**: Gets or sets the maximum number of jobs to process in a single batch
- **Default**: `AsyncEndpointsConstants.DefaultBatchSize`

#### MaximumQueueSize
- **Type**: `int`
- **Description**: Gets or sets the maximum size of the job queue
- **Default**: `AsyncEndpointsConstants.DefaultMaximumQueueSize`

### Example
```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.WorkerConfigurations.MaximumConcurrency = 8;
    options.WorkerConfigurations.PollingIntervalMs = 2000;
    options.WorkerConfigurations.JobTimeoutMinutes = 30;
    options.WorkerConfigurations.BatchSize = 10;
    options.WorkerConfigurations.MaximumQueueSize = 1000;
});
```

---

## AsyncEndpointsJobManagerConfiguration

### Class Definition
```csharp
public sealed class AsyncEndpointsJobManagerConfiguration
```

### Properties

#### DefaultMaxRetries
- **Type**: `int`
- **Description**: Gets or sets the default maximum number of retries for failed jobs
- **Default**: `AsyncEndpointsConstants.MaximumRetries`

#### RetryDelayBaseSeconds
- **Type**: `double`
- **Description**: Gets or sets the base delay in seconds for job retry exponential backoff
- **Default**: `2.0`

#### JobClaimTimeout
- **Type**: `TimeSpan`
- **Description**: Gets or sets the timeout for job claims
- **Default**: `TimeSpan.FromMinutes(5)`

#### MaxConcurrentJobs
- **Type**: `int`
- **Description**: Gets or sets the maximum number of concurrent jobs that can be processed
- **Default**: `10`

#### JobPollingIntervalMs
- **Type**: `int`
- **Description**: Gets or sets the polling interval in milliseconds for job polling
- **Default**: `1000`

#### MaxClaimBatchSize
- **Type**: `int`
- **Description**: Gets or sets the maximum number of jobs to claim in a single batch
- **Default**: `10`

#### StaleJobClaimCheckInterval
- **Type**: `TimeSpan`
- **Description**: Gets or sets the interval for checking for stale job claims
- **Default**: `TimeSpan.FromMinutes(1)`

### Example
```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.JobManagerConfiguration.DefaultMaxRetries = 5;
    options.JobManagerConfiguration.RetryDelayBaseSeconds = 3.0;
    options.JobManagerConfiguration.JobClaimTimeout = TimeSpan.FromMinutes(10);
    options.JobManagerConfiguration.MaxConcurrentJobs = 20;
    options.JobManagerConfiguration.JobPollingIntervalMs = 500;
    options.JobManagerConfiguration.MaxClaimBatchSize = 5;
    options.JobManagerConfiguration.StaleJobClaimCheckInterval = TimeSpan.FromMinutes(2);
});
```

---

## AsyncEndpointsResponseConfigurations

### Class Definition
```csharp
public sealed class AsyncEndpointsResponseConfigurations
```

### Properties

#### JobSubmittedResponseFactory
- **Type**: `Func<Job, HttpContext, Task<IResult>>`
- **Description**: Gets or sets the factory function for creating responses when a job is submitted
- **Default**: `ResponseDefaults.CreateJobSubmittedResponse`

#### JobStatusResponseFactory
- **Type**: `Func<MethodResult<Job>, HttpContext, Task<IResult>>`
- **Description**: Gets or sets the factory function for creating responses when job status is requested
- **Default**: `ResponseDefaults.CreateJobStatusResponse`

#### JobSubmissionErrorResponseFactory
- **Type**: `Func<AsyncEndpointError?, HttpContext, Task<IResult>>`
- **Description**: Gets or sets the factory function for creating responses when job submission fails
- **Default**: `ResponseDefaults.CreateJobSubmissionErrorResponse`

#### ExceptionResponseFactory
- **Type**: `Func<Exception, HttpContext, Task<IResult>>`
- **Description**: Gets or sets the factory function for creating responses when exceptions occur
- **Default**: `ResponseDefaults.CreateExceptionResponse`

### Example
```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    options.ResponseConfigurations.JobSubmittedResponseFactory = async (job, context) =>
    {
        context.Response.Headers.Append("X-Async-Job-Id", job.Id.ToString());
        return Results.Accepted($"/jobs/{job.Id}", job);
    };
    
    options.ResponseConfigurations.JobStatusResponseFactory = async (jobResult, context) =>
    {
        if (jobResult.IsSuccess && jobResult.Data != null)
        {
            return Results.Ok(jobResult.Data);
        }
        return Results.NotFound("Job not found");
    };
    
    options.ResponseConfigurations.JobSubmissionErrorResponseFactory = async (error, context) =>
    {
        return Results.Problem(
            title: "Job Submission Failed",
            detail: error?.Message,
            statusCode: 500
        );
    };
    
    options.ResponseConfigurations.ExceptionResponseFactory = async (ex, context) =>
    {
        return Results.Problem(
            title: "Internal Server Error",
            detail: "An error occurred while processing the request",
            statusCode: 500
        );
    };
});
```

---

## AsyncEndpointsRecoveryConfiguration

### Class Definition
```csharp
public sealed class AsyncEndpointsRecoveryConfiguration
```

### Properties

#### EnableDistributedJobRecovery
- **Type**: `bool`
- **Description**: Gets or sets whether to enable distributed job recovery
- **Default**: `true`

#### JobTimeoutMinutes
- **Type**: `int`
- **Description**: Gets or sets the timeout in minutes for job execution before considering it stuck
- **Default**: `30`

#### RecoveryCheckIntervalSeconds
- **Type**: `int`
- **Description**: Gets or sets the interval in seconds between recovery checks
- **Default**: `300`

#### MaximumRetries
- **Type**: `int`
- **Description**: Gets or sets the maximum number of retries for recovered jobs
- **Default**: `3`

### Example
```csharp
builder.Services.AddAsyncEndpointsWorker(recoveryConfiguration =>
{
    recoveryConfiguration.EnableDistributedJobRecovery = true;
    recoveryConfiguration.JobTimeoutMinutes = 60;
    recoveryConfiguration.RecoveryCheckIntervalSeconds = 600;
    recoveryConfiguration.MaximumRetries = 5;
});
```

---

## RedisConfiguration

### Class Definition
```csharp
public class RedisConfiguration
```

### Properties

#### ConnectionString
- **Type**: `string`
- **Description**: Gets or sets the Redis connection string
- **Default**: `string.Empty`

#### ConnectRetry
- **Type**: `int`
- **Description**: Gets or sets the number of connection retry attempts
- **Default**: `3`

#### ConnectTimeout
- **Type**: `int`
- **Description**: Gets or sets the connection timeout in milliseconds
- **Default**: `5000`

#### AbortOnConnectFail
- **Type**: `bool`
- **Description**: Gets or sets whether to abort on connection failure
- **Default**: `false`

#### Password
- **Type**: `string`
- **Description**: Gets or sets the Redis password
- **Default**: `null`

#### Ssl
- **Type**: `bool`
- **Description**: Gets or sets whether to use SSL for the connection
- **Default**: `false`

#### SslHost
- **Type**: `string`
- **Description**: Gets or sets the SSL host name
- **Default**: `null`

### Example
```csharp
builder.Services.AddAsyncEndpointsRedisStore(config =>
{
    config.ConnectionString = "localhost:6379";
    config.Password = "your-redis-password";
    config.Ssl = false;
    config.ConnectRetry = 5;
    config.ConnectTimeout = 10000;
});
```