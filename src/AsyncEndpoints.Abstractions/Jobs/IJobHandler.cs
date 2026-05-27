namespace AsyncEndpoints.Abstractions.Jobs;

public interface IJobHandler<in TJob>
{
    Task HandleAsync(TJob job, CancellationToken ct = default);
}
