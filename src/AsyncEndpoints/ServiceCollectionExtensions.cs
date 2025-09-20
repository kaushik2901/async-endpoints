using AsyncEndpoints.BackgroundWorker;
using AsyncEndpoints.Contracts;
using AsyncEndpoints.InMemoryStore;
using AsyncEndpoints.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncEndpoints;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAsyncEndpoints(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<AsyncEndpointsConfigurations>();
        services.AddSingleton<IJobStore, InMemoryJobStore>();
        services.AddScoped<IAsyncEndpointRequestDelegate, AsyncEndpointRequestDelegate>();
        return services;
    }

    public static IServiceCollection AddAsyncEndpointsWorker(this IServiceCollection services)
    {
        services.AddHostedService<AsyncEndpointsBackgroundService>();
        return services;
    }

    public static IServiceCollection AddAsyncEndpointHandler<TAsyncEndpointRequestHandler, TRequest, TResponse>(this IServiceCollection services, string jobName)
        where TAsyncEndpointRequestHandler : class, IAsyncEndpointRequestHandler<TRequest, TResponse>
    {
        services.AddKeyedScoped<IAsyncEndpointRequestHandler<TRequest, TResponse>, TAsyncEndpointRequestHandler>(jobName);
        return services;
    }
}