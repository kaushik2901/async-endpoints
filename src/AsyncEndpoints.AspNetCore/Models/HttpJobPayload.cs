namespace AsyncEndpoints.AspNetCore.Models;

public sealed record HttpJobPayload
{
	public string Body { get; init; } = string.Empty;
	public Dictionary<string, List<string?>>? Headers { get; init; }
	public Dictionary<string, string?>? RouteParams { get; init; }
	public Dictionary<string, List<string?>>? QueryParams { get; init; }
}
