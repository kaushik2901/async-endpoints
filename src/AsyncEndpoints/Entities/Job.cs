using System;
using System.Collections.Generic;

namespace AsyncEndpoints.Entities;

public sealed class Job
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public Dictionary<string, List<string?>> Headers { get; set; } = [];
    public Dictionary<string, object?> RouteParams { get; set; } = [];
    public List<KeyValuePair<string, List<string?>>> QueryParams { get; set; } = [];
    public string Payload { get; init; } = string.Empty;
    public string? Result { get; set; } = null;
    public string? Exception { get; set; } = null;
    public int RetryCount { get; set; } = 0;
    public int MaxRetries { get; set; } = AsyncEndpointsConstants.MaximumRetries;
    public DateTime? RetryDelayUntil { get; set; } = null;
    public Guid? WorkerId { get; set; } = null;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; } = null;
    public DateTimeOffset? CompletedAt { get; set; } = null;
    public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsCanceled => Status == JobStatus.Canceled;

    public static Job Create(Guid id, string name, string payload)
    {
        return new Job
        {
            Id = id,
            Name = name,
            Payload = payload
        };
    }

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

    public void SetResult(string result)
    {
        Result = result;
        UpdateStatus(JobStatus.Completed);
    }

    public void SetException(string exception)
    {
        Exception = exception;
        UpdateStatus(JobStatus.Failed);
    }

    public void IncrementRetryCount()
    {
        RetryCount++;
    }

    public void SetRetryTime(DateTime delayUntil)
    {
        RetryDelayUntil = delayUntil;
    }
}
