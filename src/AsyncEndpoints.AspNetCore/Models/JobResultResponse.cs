namespace AsyncEndpoints.AspNetCore.Models;

public sealed record JobResultResponse
{
    public Guid JobId { get; init; }
    public string? Result { get; init; }
}
