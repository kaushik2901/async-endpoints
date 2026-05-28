namespace AsyncEndpoints.Abstractions.Submission;

public interface IJobSubmitter
{
	Task<Guid> SubmitAsync<T>(string jobName, T job, string? channel = null, string? partitionKey = null, CancellationToken ct = default);

	Task<Guid> SubmitRawAsync(string jobName, string payload, string? channel = null, string? partitionKey = null, CancellationToken ct = default);

	Task<Guid> SubmitAsync<T>(T job, string? channel = null, string? partitionKey = null, CancellationToken ct = default);
}
