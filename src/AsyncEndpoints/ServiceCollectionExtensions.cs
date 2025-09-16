using AsyncEndpoints.AsyncEndpointRequestHandler;
using AsyncEndpoints.Configurations;
using AsyncEndpoints.Job;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncEndpoints;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAsyncEndpoints(this IServiceCollection services)
    {
        services.AddSingleton<AsyncEndpointConfig>();
        services.AddSingleton<IJobStore, InMemoryJobStore>();
        return services;
    }

    public static IServiceCollection AddAsyncEndpointHandler<TAsyncEndpointRequestHandler, TRequest, TResponse>(this IServiceCollection services, string jobName) where TAsyncEndpointRequestHandler : class, IAsyncEndpointRequestHandler<TRequest, TResponse>
    {
        services.AddKeyedScoped<IAsyncEndpointRequestHandler<TRequest, TResponse>, TAsyncEndpointRequestHandler>(jobName);
        return services;
    }
}