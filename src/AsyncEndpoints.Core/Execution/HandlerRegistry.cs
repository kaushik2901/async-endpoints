using AsyncEndpoints.Abstractions.Jobs;
using System.Collections.Concurrent;

namespace AsyncEndpoints.Core.Execution;

public sealed class HandlerRegistry : IHandlerRegistry
{
	private readonly ConcurrentDictionary<string, Func<IServiceProvider, JobRecord, CancellationToken, Task<string?>>> _invokers = new();

	public void Register(string jobName, Func<IServiceProvider, JobRecord, CancellationToken, Task<string?>> invoker)
	{
		_invokers.TryAdd(jobName, invoker);
	}

	public Func<IServiceProvider, JobRecord, CancellationToken, Task<string?>>? GetInvoker(string jobName)
	{
		return _invokers.TryGetValue(jobName, out var invoker) ? invoker : null;
	}
}
