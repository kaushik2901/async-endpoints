namespace RedisExampleCore;

public class ExampleJobResponse
{
	public string? Status { get; set; }
	public DateTime ProcessedAt { get; set; }
	public string? OriginalMessage { get; set; }
}