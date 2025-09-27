namespace RedisExampleCore;

public class ExampleJobRequest
{
    public string? Message { get; set; }
    public int DelayInSeconds { get; set; }
}
