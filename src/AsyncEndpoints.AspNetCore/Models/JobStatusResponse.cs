namespace AsyncEndpoints.AspNetCore.Models;

public sealed record JobStatusResponse
{
	public Guid JobId { get; init; }
	public string JobName { get; init; } = string.Empty;
	public string Status { get; init; } = string.Empty;
	public string? Result { get; init; }
	public string? ErrorMessage { get; init; }
	public DateTime CreatedAt { get; init; }
	public DateTime? StartedAt { get; init; }
	public DateTime? CompletedAt { get; init; }
	public int RetryCount { get; init; }
	public int MaxRetries { get; init; }
}
