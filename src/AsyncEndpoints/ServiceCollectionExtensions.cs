using System;
using System.Text.Json.Serialization.Metadata;
using AsyncEndpoints.BackgroundWorker;
using AsyncEndpoints.Contracts;
using AsyncEndpoints.InMemoryStore;
using AsyncEndpoints.Services;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncEndpoints;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAsyncEndpoints(this IServiceCollection services, Action<AsyncEndpointsConfigurations>? configureOptions = null)
    {
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        services.AddHttpContextAccessor();
        services.AddSingleton<AsyncEndpointsConfigurations>();
        services.AddScoped<IAsyncEndpointRequestDelegate, AsyncEndpointRequestDelegate>();
        services.AddAsyncEndpointsJsonTypeInfoResolver(AsyncEndpointsJsonSerializationContext.Default);

        return services;
    }

    public static IServiceCollection AddAsyncEndpointsInMemoryStore(this IServiceCollection services)
    {
        services.AddSingleton<IJobStore, InMemoryJobStore>();

        return services;
    }

    public static IServiceCollection AddAsyncEndpointsJsonTypeInfoResolver(this IServiceCollection services, IJsonTypeInfoResolver jsonTypeInfoResolver)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Add(jsonTypeInfoResolver);
        });

        return services;
    }

    public static IServiceCollection AddAsyncEndpointsWorker(this IServiceCollection services)
    {
        services.AddTransient<IJobConsumerService, JobConsumerService>();
        services.AddTransient<IJobProducerService, JobProducerService>();
        services.AddTransient<IJobProcessorService, JobProcessorService>();
        services.AddTransient<IHandlerExecutionService, HandlerExecutionService>();
        services.AddHostedService<AsyncEndpointsBackgroundService>();

        return services;
    }

    public static IServiceCollection AddAsyncEndpointHandler<TAsyncEndpointRequestHandler, TRequest, TResponse>(this IServiceCollection services, string jobName)
        where TAsyncEndpointRequestHandler : class, IAsyncEndpointRequestHandler<TRequest, TResponse>
    {
        services.AddKeyedScoped<IAsyncEndpointRequestHandler<TRequest, TResponse>, TAsyncEndpointRequestHandler>(jobName);

        HandlerRegistrationTracker.Register<TRequest, TResponse>(jobName,
            async (serviceProvider, request, job, cancellationToken) =>
            {
                var handler = serviceProvider.GetRequiredKeyedService<IAsyncEndpointRequestHandler<TRequest, TResponse>>(jobName);
                var context = AsyncContextBuilder.Build(request, job);
                return await handler.HandleAsync(context, cancellationToken);
            });

        return services;
    }
}