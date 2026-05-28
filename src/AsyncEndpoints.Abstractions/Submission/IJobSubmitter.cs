namespace AsyncEndpoints.Abstractions.Submission;

public interface IJobSubmitter
{
	Task<Guid> SubmitAsync(string jobName, string payload, string? channel = null, string? partitionKey = null, CancellationToken ct = default);
}
