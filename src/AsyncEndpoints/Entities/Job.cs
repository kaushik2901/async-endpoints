using System;

namespace AsyncEndpoints.Entities;

public sealed class Job
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public string Payload { get; init; } = string.Empty;
    public string? Result { get; set; } = null;
    public string? Exception { get; set; } = null;
    public int RetryCount { get; set; } = 0;
    public int MaxRetries { get; set; } = AsyncEndpointsConstants.MaximumRetries;
    public DateTime? RetryDelayUntil { get; set; } = null;
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
    }

    public void SetException(string exception)
    {
        Exception = exception;
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
