using AsyncEndpoints.Abstractions.Jobs;

namespace AsyncEndpoints.Execution;

public interface IHandlerRegistry
{
    void Register<T>(string jobName, Func<IServiceProvider, JobRecord, CancellationToken, Task> invoker);
    Func<IServiceProvider, JobRecord, CancellationToken, Task>? GetInvoker(string jobName);
}
