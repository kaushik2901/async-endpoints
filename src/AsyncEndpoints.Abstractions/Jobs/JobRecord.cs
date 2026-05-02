namespace AsyncEndpoints.Abstractions.Jobs;

/// <summary>
/// Represents the persisted state of a job in the system.
/// </summary>
public record JobRecord
{
	public Guid JobId { get; init; } = Guid.NewGuid();
	public string Channel { get; init; } = "default";
	public int Priority { get; init; } = 0;
	public string PayloadType { get; init; } = string.Empty;
	public string Payload { get; init; } = string.Empty;
	public JobStatus Status { get; init; } = JobStatus.Queued;
	public int RetryCount { get; init; } = 0;
	public int MaxRetries { get; init; } = 3;
	public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
	public DateTimeOffset? RunAfter { get; init; }
	public DateTimeOffset? StartedAt { get; init; }
	public DateTimeOffset? CompletedAt { get; init; }
	public string? WorkerId { get; init; }
	public DateTimeOffset? LastHeartbeat { get; init; }
	public string? Result { get; init; }
	public string? ErrorMessage { get; init; }
	public string? PartitionKey { get; init; }
}
