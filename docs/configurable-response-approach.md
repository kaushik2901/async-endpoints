# Configurable Response Approach for AsyncEndpoints

## Overview
This document outlines the approach for implementing configurable responses in AsyncEndpoints similar to how FluentValidation allows developers to customize validation error responses.

## Current State
Currently, AsyncEndpoints returns a standard `JobResponse` containing basic job information such as:
- Job ID
- Job Name  
- Job Status
- Timestamps
- Result payload (when available)
- Error details (when failed)

The response format is hardcoded in `AsyncEndpointRequestDelegate.cs` and returned as `Results.Accepted("", jobResponse)`.

## Desired Behavior
Developers should be able to customize:
1. The response format when a job is successfully submitted
2. The response format for individual job status queries
3. Error response formats
4. The HTTP status code returned when submitting jobs

Similar to how FluentValidation provides `CustomizeValidatorOptions` and `CustomizeOptions`, AsyncEndpoints should provide a fluent configuration API.

## Proposed Solution

### 1. Configuration Class
Create a new `AsyncEndpointsResponseConfigurations` class with customizable response delegates:

```csharp
public sealed class AsyncEndpointsResponseConfigurations
{
    public Func<Job, HttpContext, Task<IResult>> JobSubmittedResponseFactory { get; set; }
    public Func<Job, HttpContext, Task<IResult>> JobStatusResponseFactory { get; set; }
    public Func<AsyncEndpointError?, HttpContext, Task<IResult>> JobSubmissionErrorResponseFactory { get; set; }
    public Func<Exception, HttpContext, Task<IResult>> ExceptionResponseFactory { get; set; }
    
    public AsyncEndpointsResponseConfigurations()
    {
        // Set default implementations to avoid null checks
        JobSubmittedResponseFactory = Utilities.ResponseDefaults.DefaultJobSubmittedResponseFactory;
        JobStatusResponseFactory = Utilities.ResponseDefaults.DefaultJobStatusResponseFactory;
        JobSubmissionErrorResponseFactory = Utilities.ResponseDefaults.DefaultJobSubmissionErrorResponseFactory;
        ExceptionResponseFactory = Utilities.ResponseDefaults.DefaultExceptionResponseFactory;
    }
}

namespace AsyncEndpoints.Utilities;

public static class ResponseDefaults
{
    public static async Task<IResult> DefaultJobSubmittedResponseFactory(Job job, HttpContext context)
    {
        var jobResponse = JobResponseMapper.ToResponse(job);
        return Results.Accepted("", jobResponse);
    }
    
    public static async Task<IResult> DefaultJobStatusResponseFactory(Job job, HttpContext context)
    {
        var jobResponse = JobResponseMapper.ToResponse(job);
        return Results.Ok(jobResponse);
    }
    
    public static async Task<IResult> DefaultJobSubmissionErrorResponseFactory(AsyncEndpointError? error, HttpContext context)
    {
        return Results.Problem(
            detail: error?.Message ?? "An unknown error occurred while submitting the job",
            title: "Job Submission Failed",
            statusCode: 500
        );
    }
    
    public static async Task<IResult> DefaultExceptionResponseFactory(Exception exception, HttpContext context)
    {
        return Results.Problem(
            detail: exception.Message,
            title: "An error occurred",
            statusCode: 500
        );
    }
}
```

### 2. Integration with Existing Configuration
Add the new response configurations to the existing `AsyncEndpointsConfigurations`:

```csharp
public sealed class AsyncEndpointsConfigurations
{
    public AsyncEndpointsWorkerConfigurations WorkerConfigurations { get; set; } = new();
    public AsyncEndpointsJobManagerConfiguration JobManagerConfiguration { get; set; } = new();
    public AsyncEndpointsResponseConfigurations ResponseConfigurations { get; set; } = new();
}
```

### 3. Service Registration Extension
Enhance the service registration to allow fluent configuration of response formats:

```csharp
services.AddAsyncEndpoints(config =>
{
    config.ResponseConfigurations.JobSubmittedResponseFactory = async (job, context) =>
    {
        var response = new CustomJobResponse 
        { 
            JobId = job.Id, 
            StatusUrl = $"/api/jobs/{job.Id}/status",
            Message = "Your request has been accepted for processing"
        };
        return Results.Ok(response);
    };

    config.ResponseConfigurations.JobSubmissionErrorResponseFactory = async (error, context) =>
    {
        return Results.Problem(
            detail: error?.Message ?? "An unknown error occurred",
            title: "Job Submission Failed",
            statusCode: error?.StatusCode ?? 422, // Use custom status code from error or default to 422
            extensions: new Dictionary<string, object?>
            {
                ["error_code"] = error?.Code,
                ["exception_type"] = error?.Exception?.Type
            }
        );
    };
});
```

### 4. Updated AsyncEndpointRequestDelegate
Modify the `AsyncEndpointRequestDelegate` to use the configured response factories:

```csharp
public async Task<IResult> HandleAsync<TRequest>(
    string jobName,
    HttpContext httpContext,
    TRequest request,
    Func<HttpContext, TRequest, CancellationToken, Task<IResult?>?>? handler = null,
    CancellationToken cancellationToken = default)
{
    var handlerResponse = await HandleRequestDelegate(handler, httpContext, request, cancellationToken);
    if (handlerResponse != null)
    {
        _logger.LogDebug("Handler provided direct response for job: {JobName}", jobName);
        return handlerResponse;
    }

    var payload = _serializer.Serialize(request);
    _logger.LogDebug("Serialized request payload for job: {JobName}", jobName);

    var submitJobResult = await _jobManager.SubmitJob(jobName, payload, httpContext, cancellationToken);
    if (!submitJobResult.IsSuccess)
    {
        _logger.LogError("Failed to submit job {JobName}: {ErrorMessage}", jobName, submitJobResult.Error?.Message);

        if (submitJobResult.Error?.Exception != null)
        {
            _logger.LogCritical("Exception occurred while submitting job {JobName}: Type={ExceptionType}, Message={ExceptionMessage}, StackTrace={StackTrace}",
                jobName,
                submitJobResult.Error.Exception.Type,
                submitJobResult.Error.Exception.Message,
                submitJobResult.Error.Exception.StackTrace);
        }

        // Use configured error response factory (no null check needed)
        return await _responseConfigurations.JobSubmissionErrorResponseFactory(
            submitJobResult.Error,
            httpContext);
    }

    var job = submitJobResult.Data!;

    // Use configured success response factory (no null check needed)
    return await _responseConfigurations.JobSubmittedResponseFactory(job, httpContext);
}
```

### 5. Job Status Response Customization
Update the endpoint that retrieves job status to use the configured response factory:

```csharp
// In RouteBuilderExtensions.cs
.MapGet(pattern, (HttpContext httpContext, [FromRoute] Guid jobId, 
                  [FromServices] IJobManager jobManager, 
                  CancellationToken cancellationToken) =>
{
    return HandleJobDetailsRequest(jobManager, jobId, httpContext, cancellationToken);
})

private static async Task<IResult> HandleJobDetailsRequest(IJobManager jobManager, Guid jobId, HttpContext httpContext, CancellationToken cancellationToken)
{
    var result = await jobManager.GetJobById(jobId, cancellationToken);
    
    if (!result.IsSuccess || result.Data == null)
    {
        return Results.NotFound(new { Message = $"Job with ID {jobId} not found" });
    }

    var job = result.Data;
    var configurations = httpContext.RequestServices.GetRequiredService<AsyncEndpointsConfigurations>();
    
    // Use configured job status response factory (no null check needed)
    return await configurations.ResponseConfigurations.JobStatusResponseFactory(job, httpContext);
}
```

## Benefits

1. **Flexibility**: Developers can return custom response formats that match their API contracts
2. **Consistency**: Provides a consistent way to customize responses across the library
3. **Backwards Compatibility**: Default implementations maintain existing behavior
4. **Fluent API**: Similar to FluentValidation's approach, making it familiar to .NET developers
5. **Error Handling**: Allows customization of error responses including HTTP status codes

## Implementation Steps

1. Create the `AsyncEndpointsResponseConfigurations` class
2. Update `AsyncEndpointsConfigurations` to include response configurations
3. Modify `AsyncEndpointRequestDelegate` to use response factories
4. Update service registration extensions to allow configuration
5. Update job status retrieval to use response factory
6. Add comprehensive documentation and examples
7. Add unit tests for the new functionality

## Migration Path

The changes would be fully backward compatible since default response factories would maintain the existing behavior. Existing implementations would continue to work without modification.