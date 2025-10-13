namespace RedisExampleCore;

public class ExampleResponse
{
    public int Id { get; set; }
    public string? Message { get; set; }
    public int Status { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string? OriginalName { get; set; }
}
