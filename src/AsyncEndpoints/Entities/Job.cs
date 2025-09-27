using System;
using System.Collections.Generic;

namespace AsyncEndpoints.Entities;

/// <summary>
/// Represents an asynchronous job in the AsyncEndpoints system.
/// </summary>
public sealed class Job
{
    /// <summary>
    /// Gets the unique identifier of the job.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the name of the job.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current status of the job.
    /// </summary>
    public JobStatus Status { get; set; } = JobStatus.Queued;

    /// <summary>
    /// Gets or sets the collection of HTTP headers associated with the job.
    /// </summary>
    public Dictionary<string, List<string?>> Headers { get; set; } = [];

    /// <summary>
    /// Gets or sets the route parameters associated with the job.
    /// </summary>
    public Dictionary<string, object?> RouteParams { get; set; } = [];

    /// <summary>
    /// Gets or sets the query parameters associated with the job.
    /// </summary>
    public List<KeyValuePair<string, List<string?>>> QueryParams { get; set; } = [];

    /// <summary>
    /// Gets the payload data for the job.
    /// </summary>
    public string Payload { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the result of the job execution, if successful.
    /// </summary>
    public string? Result { get; set; } = null;

    /// <summary>
    /// Gets or sets the exception details if the job failed.
    /// </summary>
    public string? Exception { get; set; } = null;

    /// <summary>
    /// Gets or sets the number of times the job has been retried.
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Gets or sets the maximum number of retries allowed for the job.
    /// </summary>
    public int MaxRetries { get; set; } = AsyncEndpointsConstants.MaximumRetries;

    /// <summary>
    /// Gets or sets the time until which the job is scheduled for retry.
    /// </summary>
    public DateTime? RetryDelayUntil { get; set; } = null;

    /// <summary>
    /// Gets or sets the ID of the worker currently processing this job, if any.
    /// </summary>
    public Guid? WorkerId { get; set; } = null;

    /// <summary>
    /// Gets or sets the date and time when the job was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the date and time when the job processing started, if applicable.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; } = null;

    /// <summary>
    /// Gets or sets the date and time when the job processing completed, if applicable.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; } = null;

    /// <summary>
    /// Gets or sets the date and time when the job was last updated.
    /// </summary>
    public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets a value indicating whether the job has been canceled.
    /// </summary>
    public bool IsCanceled => Status == JobStatus.Canceled;

    /// <summary>
    /// Creates a new job with the specified parameters.
    /// </summary>
    /// <param name="id">The unique identifier for the job.</param>
    /// <param name="name">The name of the job.</param>
    /// <param name="payload">The payload data for the job.</param>
    /// <returns>A new <see cref="Job"/> instance.</returns>
    public static Job Create(Guid id, string name, string payload)
    {
        return new Job
        {
            Id = id,
            Name = name,
            Payload = payload
        };
    }

    /// <summary>
    /// Creates a new job with the specified parameters including HTTP context information.
    /// </summary>
    /// <param name="id">The unique identifier for the job.</param>
    /// <param name="name">The name of the job.</param>
    /// <param name="payload">The payload data for the job.</param>
    /// <param name="headers">The HTTP headers associated with the original request.</param>
    /// <param name="routeParams">The route parameters associated with the original request.</param>
    /// <param name="queryParams">The query parameters associated with the original request.</param>
    /// <returns>A new <see cref="Job"/> instance.</returns>
    public static Job Create(Guid id, string name, string payload, Dictionary<string, List<string?>> headers, Dictionary<string, object?> routeParams, List<KeyValuePair<string, List<string?>>> queryParams)
    {
        return new Job
        {
            Id = id,
            Name = name,
            Payload = payload,
            Headers = headers,
            RouteParams = routeParams,
            QueryParams = queryParams
        };
    }

    /// <summary>
    /// Updates the status of the job and updates the last updated timestamp.
    /// </summary>
    /// <param name="status">The new status to set for the job.</param>
    public void UpdateStatus(JobStatus status)
    {
        Status = status;
        LastUpdatedAt = DateTimeOffset.UtcNow;

        switch (status)
        {
            case JobStatus.InProgress:
                StartedAt = DateTimeOffset.UtcNow;
                break;
            case JobStatus.Completed:
            case JobStatus.Failed:
            case JobStatus.Canceled:
                CompletedAt = DateTimeOffset.UtcNow;
                break;
        }
    }

    /// <summary>
    /// Sets the result of the job and updates the status to completed.
    /// </summary>
    /// <param name="result">The result of the job execution.</param>
    public void SetResult(string result)
    {
        Result = result;
        UpdateStatus(JobStatus.Completed);
    }

    /// <summary>
    /// Sets the exception details for the job and updates the status to failed.
    /// </summary>
    /// <param name="exception">The exception that occurred during job execution.</param>
    public void SetException(string exception)
    {
        Exception = exception;
        UpdateStatus(JobStatus.Failed);
    }

    /// <summary>
    /// Increments the retry count for the job.
    /// </summary>
    public void IncrementRetryCount()
    {
        RetryCount++;
    }

    /// <summary>
    /// Sets the retry delay time for the job.
    /// </summary>
    /// <param name="delayUntil">The time until which the job is scheduled for retry.</param>
    public void SetRetryTime(DateTime delayUntil)
    {
        RetryDelayUntil = delayUntil;
    }
}
