namespace AsyncEndpoints.AspNetCore.Models;

public sealed record JobSubmittedResponse
{
	public Guid JobId { get; init; }
}
