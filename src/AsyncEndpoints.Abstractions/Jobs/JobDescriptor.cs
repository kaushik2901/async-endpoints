namespace AsyncEndpoints.Abstractions.Jobs;

public record JobDescriptor(
	string JobName,
	string Payload,
	string? Channel = "default",
	int Priority = 0,
	string? PartitionKey = null,
	Dictionary<string, string>? Metadata = null
);
