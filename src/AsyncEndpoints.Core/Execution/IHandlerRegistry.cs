using AsyncEndpoints.Abstractions.Jobs;

namespace AsyncEndpoints.Core.Execution;

public interface IHandlerRegistry
{
	void Register<T>(string jobName, Func<IServiceProvider, JobRecord, CancellationToken, Task> invoker);
	Func<IServiceProvider, JobRecord, CancellationToken, Task>? GetInvoker(string jobName);
}
