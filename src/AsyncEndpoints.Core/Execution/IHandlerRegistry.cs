using AsyncEndpoints.Abstractions.Jobs;

namespace AsyncEndpoints.Core.Execution;

public interface IHandlerRegistry
{
	void Register(string jobName, Func<IServiceProvider, JobRecord, CancellationToken, Task<string?>> invoker);
	Func<IServiceProvider, JobRecord, CancellationToken, Task<string?>>? GetInvoker(string jobName);
}
