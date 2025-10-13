namespace RedisExampleCore;

public class ExampleRequest
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int ProcessingDelaySeconds { get; set; } = 0;
}
