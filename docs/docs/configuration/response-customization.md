---
sidebar_position: 5
title: Response Customization
---

# Response Customization

This page details how to customize HTTP responses in AsyncEndpoints using the flexible response configuration system.

## Overview

AsyncEndpoints provides a comprehensive response customization system that allows you to control the HTTP responses returned to clients at various stages of the async workflow. This includes responses for job submission, job status queries, and error conditions.

## Response Configuration Properties

### JobSubmittedResponseFactory
- **Type**: `Func<Job, HttpContext, Task<IResult>>`
- **Default**: Returns `202 Accepted` with job details
- **Description**: Factory method for customizing responses when a job is successfully submitted

```csharp
options.ResponseConfigurations.JobSubmittedResponseFactory = async (job, context) =>
{
    // Add custom headers
    context.Response.Headers.Append("Async-Job-Id", job.Id.ToString());
    context.Response.Headers.Append("X-Processing-Time", DateTime.UtcNow.ToString("O"));
    
    // Return accepted response with location header
    return Results.Accepted($"/jobs/{job.Id}", job);
};
```

### JobStatusResponseFactory
- **Type**: `Func<MethodResult<Job>, HttpContext, Task<IResult>>`
- **Default**: Returns appropriate response based on job status
- **Description**: Factory method for customizing responses when querying job status

```csharp
options.ResponseConfigurations.JobStatusResponseFactory = async (jobResult, context) =>
{
    if (jobResult.IsSuccess && jobResult.Data != null)
    {
        var job = jobResult.Data;
        
        // Customize response based on job status
        return job.Status switch
        {
            JobStatus.Completed => Results.Ok(job),
            JobStatus.Failed => Results.Problem(
                title: "Job Failed",
                detail: job.Error?.Message,
                statusCode: 500
            ),
            JobStatus.Canceled => Results.StatusCode(410), // Gone
            _ => Results.Ok(job) // Queued, InProgress, Scheduled
        };
    }
    
    // Job not found
    return Results.Problem("Job not found", statusCode: 404);
};
```

### JobSubmissionErrorResponseFactory
- **Type**: `Func<AsyncEndpointError?, HttpContext, Task<IResult>>`
- **Default**: Returns appropriate error response
- **Description**: Factory method for customizing responses when job submission fails

```csharp
options.ResponseConfigurations.JobSubmissionErrorResponseFactory = async (error, context) =>
{
    if (error != null)
    {
        return Results.Problem(
            title: "Job Submission Failed",
            detail: error.Message,
            statusCode: 500,
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = error.Code,
                ["exceptionType"] = error.Exception?.Type
            }
        );
    }
    
    return Results.Problem("Unknown error during job submission", statusCode: 500);
};
```

### ExceptionResponseFactory
- **Type**: `Func<Exception, HttpContext, Task<IResult>>`
- **Default**: Returns 500 Internal Server Error
- **Description**: Factory method for customizing responses when exceptions occur

```csharp
options.ResponseConfigurations.ExceptionResponseFactory = async (exception, context) =>
{
    // Log the exception
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogError(exception, "Unhandled exception in async endpoint");
    
    // Return custom error response
    return Results.Problem(
        title: "Internal Server Error",
        detail: "An error occurred while processing the request",
        statusCode: 500
    );
};
```

## Response Customization Examples

### Basic Customization

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    // Custom job submitted response
    options.ResponseConfigurations.JobSubmittedResponseFactory = async (job, context) =>
    {
        context.Response.Headers.Append("X-Async-Job-Id", job.Id.ToString());
        return Results.Created($"/jobs/{job.Id}", new { jobId = job.Id, status = job.Status });
    };
    
    // Custom status response
    options.ResponseConfigurations.JobStatusResponseFactory = async (jobResult, context) =>
    {
        if (jobResult.IsSuccess && jobResult.Data != null)
        {
            var job = jobResult.Data;
            // Return simplified response
            return Results.Ok(new
            {
                jobId = job.Id,
                status = job.Status,
                completed = job.Status == JobStatus.Completed,
                hasResult = job.Result != null
            });
        }
        
        return Results.NotFound();
    };
});
```

### Advanced Customization with Monitoring

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    // Job submitted response with monitoring headers
    options.ResponseConfigurations.JobSubmittedResponseFactory = async (job, context) =>
    {
        // Add monitoring headers
        context.Response.Headers.Append("X-Async-Job-Id", job.Id.ToString());
        context.Response.Headers.Append("X-Async-Job-Name", job.Name);
        context.Response.Headers.Append("X-Async-Queue-Time", DateTimeOffset.UtcNow.ToString("O"));
        
        // Log for monitoring
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Submitted job {JobId} for {JobName}", job.Id, job.Name);
        
        return Results.Accepted($"/jobs/{job.Id}", new
        {
            jobId = job.Id,
            jobName = job.Name,
            location = $"/jobs/{job.Id}",
            status = job.Status,
            createdAt = job.CreatedAt
        });
    };
    
    // Status response with performance metrics
    options.ResponseConfigurations.JobStatusResponseFactory = async (jobResult, context) =>
    {
        if (jobResult.IsSuccess && jobResult.Data != null)
        {
            var job = jobResult.Data;
            
            // Calculate processing metrics
            var response = new
            {
                jobId = job.Id,
                status = job.Status,
                retryCount = job.RetryCount,
                maxRetries = job.MaxRetries,
                timestamps = new
                {
                    createdAt = job.CreatedAt,
                    startedAt = job.StartedAt,
                    completedAt = job.CompletedAt,
                    lastUpdated = job.LastUpdatedAt
                },
                hasError = job.Error != null,
                hasResult = job.Result != null
            };
            
            // Add performance metrics to headers
            if (job.StartedAt.HasValue)
            {
                var processingTime = job.CompletedAt?.Subtract(job.StartedAt.Value);
                if (processingTime.HasValue)
                {
                    context.Response.Headers.Append("X-Processing-Time", processingTime.Value.TotalMilliseconds.ToString());
                }
            }
            
            return Results.Ok(response);
        }
        
        return Results.NotFound(new { error = "Job not found", jobId = context.Request.RouteValues["jobId"] });
    };
    
    // Error response with detailed information
    options.ResponseConfigurations.JobSubmissionErrorResponseFactory = async (error, context) =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("Job submission failed: {ErrorMessage}", error?.Message);
        
        return Results.Problem(
            title: "Job Submission Failed",
            detail: error?.Message ?? "Unknown error during job submission",
            statusCode: 422, // Unprocessable Entity
            extensions: new Dictionary<string, object?>
            {
                ["timestamp"] = DateTime.UtcNow,
                ["errorCode"] = error?.Code,
                ["errorType"] = error?.Exception?.Type
            }
        );
    };
});
```

### Security-Enhanced Responses

```csharp
builder.Services.AddAsyncEndpoints(options =>
{
    // Secure job submitted response
    options.ResponseConfigurations.JobSubmittedResponseFactory = async (job, context) =>
    {
        // Don't expose internal details in response
        return Results.Accepted($"/jobs/{job.Id}", new
        {
            id = job.Id,
            status = job.Status,
            createdAt = job.CreatedAt
        });
    };
    
    // Secure status response
    options.ResponseConfigurations.JobStatusResponseFactory = async (jobResult, context) =>
    {
        if (jobResult.IsSuccess && jobResult.Data != null)
        {
            var job = jobResult.Data;
            
            // Only return necessary information
            var response = new
            {
                id = job.Id,
                status = job.Status,
                createdAt = job.CreatedAt,
                startedAt = job.StartedAt,
                completedAt = job.CompletedAt,
                isComplete = job.Status == JobStatus.Completed
            };
            
            // Add authentication-based access control if needed
            // (Implementation would check user permissions)
            
            return Results.Ok(response);
        }
        
        // Don't reveal if job exists to unauthorized users
        return Results.NotFound();
    };
});
```

## HTTP Status Code Conventions

AsyncEndpoints follows these HTTP status code conventions:

- **202 Accepted**: Job successfully submitted to queue
- **200 OK**: Job status information returned successfully
- **404 Not Found**: Job does not exist
- **422 Unprocessable Entity**: Job submission validation failed
- **500 Internal Server Error**: Unexpected error occurred
- **410 Gone**: Job was canceled

### Custom Status Code Mapping

```csharp
options.ResponseConfigurations.JobStatusResponseFactory = async (jobResult, context) =>
{
    if (jobResult.IsSuccess && jobResult.Data != null)
    {
        var job = jobResult.Data;
        
        return job.Status switch
        {
            JobStatus.Completed => Results.Ok(job),
            JobStatus.InProgress => Results.Accepted(job),
            JobStatus.Queued => Results.Accepted(job),
            JobStatus.Scheduled => Results.Accepted(job),
            JobStatus.Failed => Results.StatusCode(500),
            JobStatus.Canceled => Results.StatusCode(410),
            _ => Results.Ok(job)
        };
    }
    
    return Results.NotFound();
};
```

## Response Format Customization

### JSON Format Customization

```csharp
options.ResponseConfigurations.JobSubmittedResponseFactory = async (job, context) =>
{
    // Custom JSON structure
    var response = new
    {
        id = job.Id,
        status = job.Status,
        type = "async-operation",
        links = new
        {
            status = $"/jobs/{job.Id}",
            self = $"/jobs/{job.Id}"
        },
        meta = new
        {
            createdAt = job.CreatedAt,
            queuePosition = 1 // Would need to calculate in real implementation
        }
    };
    
    return Results.Json(response, statusCode: 202);
};
```

### XML Response Format (if needed)

```csharp
options.ResponseConfigurations.JobSubmittedResponseFactory = async (job, context) =>
{
    // Create XML response
    var xmlString = $@"
        <job>
            <id>{job.Id}</id>
            <status>{job.Status}</status>
            <createdAt>{job.CreatedAt:O}</createdAt>
        </job>";
    
    context.Response.ContentType = "application/xml";
    await context.Response.WriteAsync(xmlString);
    
    return Results.Extensions.StatusCode(202);
};
```

## Performance Monitoring Integration

### Response Time Tracking

```csharp
options.ResponseConfigurations.JobSubmittedResponseFactory = async (job, context) =>
{
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    
    // Add response headers
    context.Response.Headers.Append("X-Request-Id", Guid.NewGuid().ToString());
    context.Response.Headers.Append("X-Processing-Time", stopwatch.ElapsedMilliseconds.ToString());
    
    var result = Results.Accepted($"/jobs/{job.Id}", job);
    
    // Log performance metrics
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Job submission completed in {ElapsedMs}ms for job {JobId}", 
        stopwatch.ElapsedMilliseconds, job.Id);
    
    return result;
};
```

## Error Response Enhancement

### Detailed Error Information

```csharp
options.ResponseConfigurations.JobSubmissionErrorResponseFactory = async (error, context) =>
{
    var errorResponse = new
    {
        error = new
        {
            code = error?.Code ?? "UNKNOWN_ERROR",
            message = error?.Message ?? "An unknown error occurred",
            details = error?.Exception?.Message,
            stackTrace = error?.Exception?.StackTrace,
            timestamp = DateTime.UtcNow,
            requestId = context.TraceIdentifier
        }
    };
    
    return Results.Json(errorResponse, statusCode: 500);
};
```

## Conditional Response Customization

### Environment-Based Responses

```csharp
if (builder.Environment.IsDevelopment())
{
    options.ResponseConfigurations.ExceptionResponseFactory = async (exception, context) =>
    {
        // Include detailed exception info in development
        return Results.Problem(
            title: "Internal Server Error",
            detail: exception.Message,
            statusCode: 500,
            extensions: new Dictionary<string, object?>
            {
                ["stackTrace"] = exception.StackTrace
            }
        );
    };
}
else
{
    options.ResponseConfigurations.ExceptionResponseFactory = async (exception, context) =>
    {
        // Minimal error details in production
        return Results.Problem(
            title: "Internal Server Error",
            detail: "An error occurred while processing the request",
            statusCode: 500
        );
    };
}
```

## Best Practices for Response Customization

### Keep Responses Consistent

Maintain consistent response formats across your API for predictability.

### Don't Expose Internal Details

Avoid exposing internal implementation details in responses, especially in production.

### Include Relevant Metadata

Add helpful metadata like timestamps, request IDs, and processing information.

### Follow HTTP Semantics

Use appropriate HTTP status codes that reflect the operation outcome.

### Handle Errors Gracefully

Provide meaningful error messages that help clients understand what went wrong.

### Monitor Performance

Include performance-related headers for monitoring and debugging.

Response customization allows you to tailor AsyncEndpoints output to match your application's requirements while maintaining the robust async processing functionality.