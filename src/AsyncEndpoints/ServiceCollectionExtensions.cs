using Microsoft.Extensions.DependencyInjection;

namespace AsyncEndpoints;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAsyncEndpoints(this IServiceCollection services)
    {
        services.AddSingleton<AsyncEndpointConfig>();
        return services;
    }
}