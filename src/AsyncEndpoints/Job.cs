using System;

namespace AsyncEndpoints;

public class Job
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public string Payload { get; init; } = string.Empty;
    public string? Result { get; set; } = null;
    public string? Exception { get; set; } = null;
    public int RetryCount { get; set; } = 0;
    public int MaxRetries { get; set; } = 3;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; } = null;
    public DateTimeOffset? CompletedAt { get; set; } = null;
    public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsCanceled => Status == JobStatus.Canceled;

    public static Job Create(Guid id, string payload)
    {
        return new Job
        {
            Id = id,
            Payload = payload
        };
    }
}
