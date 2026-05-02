namespace AsyncEndpoints.Abstractions.Jobs;

/// <summary>
/// Represents a job to be submitted to the system.
/// </summary>
public record JobDescriptor
{
	public Guid JobId { get; init; } = Guid.NewGuid();
	public string Channel { get; init; } = "default";
	public int Priority { get; init; } = 0;
	public string PayloadType { get; init; } = string.Empty;
	public string Payload { get; init; } = string.Empty;
	public int MaxRetries { get; init; } = 3;
	public DateTimeOffset? RunAfter { get; init; }
	public string? PartitionKey { get; init; }
}
