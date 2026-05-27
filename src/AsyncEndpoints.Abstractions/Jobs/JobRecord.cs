namespace AsyncEndpoints.Abstractions.Jobs;

public class JobRecord
{
    public Guid JobId { get; init; }
    public string JobName { get; init; } = string.Empty;
    public string Channel { get; init; } = "default";
    public int Priority { get; init; }
    public int? Partition { get; init; }
    public string Payload { get; init; } = string.Empty;
    public JobStatus Status { get; init; }
    public int RetryCount { get; init; }
    public int MaxRetries { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? WorkerId { get; init; }
    public DateTime? LastHeartbeat { get; init; }
    public string? Result { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}
