using AsyncEndpoints.Core.Execution;
using AsyncEndpoints.Core.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncEndpoints.Core.Internal;

/// <summary>
/// Registry mapping job type names to their System.Type and executor factories.
/// </summary>
public class JobTypeRegistry
{
	private readonly Dictionary<string, Type> _jobTypes = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, Func<IServiceProvider, IJobExecutor>> _executorFactories = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Registers a job type.
	/// </summary>
	public void Register<TJob>()
	{
		var key = typeof(TJob).Name;
		_jobTypes[key] = typeof(TJob);
		_executorFactories[key] = sp => sp.GetRequiredService<JobExecutor<TJob>>();
	}

	/// <summary>
	/// Gets the job type for the given type name.
	/// </summary>
	public Type GetJobType(string typeName)
	{
		if (!_jobTypes.TryGetValue(typeName, out var type))
		{
			throw new UnknownJobTypeException(typeName);
		}
		return type;
	}

	/// <summary>
	/// Resolves the executor for the given type name from the service provider.
	/// </summary>
	public IJobExecutor GetExecutor(string typeName, IServiceProvider serviceProvider)
	{
		if (!_executorFactories.TryGetValue(typeName, out var factory))
		{
			throw new UnknownJobTypeException(typeName);
		}
		return factory(serviceProvider);
	}

	/// <summary>
	/// Checks if a job type is registered.
	/// </summary>
	public bool IsRegistered(string typeName) => _jobTypes.ContainsKey(typeName);
}
