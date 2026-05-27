using AsyncEndpoints.Utilities;

namespace AsyncEndpoints.AspNetCore.Models;

public sealed class JobResponse
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public int RetryCount { get; set; }
	public int MaxRetries { get; set; }
	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset? StartedAt { get; set; }
	public DateTimeOffset? CompletedAt { get; set; }
	public DateTimeOffset LastUpdatedAt { get; set; }
	public string Result { get; set; } = string.Empty;
	public AsyncEndpointError? Error { get; set; } = null;
}
