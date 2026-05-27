using AsyncEndpoints.Abstractions.Jobs;
using System.Collections.Concurrent;

namespace AsyncEndpoints.Execution;

public sealed class HandlerRegistry : IHandlerRegistry
{
    private readonly ConcurrentDictionary<string, Func<IServiceProvider, JobRecord, CancellationToken, Task>> _invokers = new();

    public void Register<T>(string jobName, Func<IServiceProvider, JobRecord, CancellationToken, Task> invoker)
    {
        _invokers.TryAdd(jobName, invoker);
    }

    public Func<IServiceProvider, JobRecord, CancellationToken, Task>? GetInvoker(string jobName)
    {
        return _invokers.TryGetValue(jobName, out var invoker) ? invoker : null;
    }
}
