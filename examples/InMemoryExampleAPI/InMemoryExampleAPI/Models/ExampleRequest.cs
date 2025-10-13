namespace InMemoryExampleAPI.Models;

public class ExampleRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ProcessingDelaySeconds { get; set; } = 0;
}
