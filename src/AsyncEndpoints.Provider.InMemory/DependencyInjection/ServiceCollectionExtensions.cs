using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.Provider.InMemory.JobProcessing;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncEndpoints.Provider.InMemory.DependencyInjection;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddAsyncEndpointsInMemory(this IServiceCollection services)
	{
		services.AddSingleton<IJobStore, InMemoryJobStore>();
		return services;
	}
}
