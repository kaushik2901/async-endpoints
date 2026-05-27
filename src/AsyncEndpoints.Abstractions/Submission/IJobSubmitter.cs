namespace AsyncEndpoints.Abstractions.Submission;

public interface IJobSubmitter
{
    Task<Guid> SubmitAsync<T>(T job, string? channel = null, string? partitionKey = null, CancellationToken ct = default);
}
