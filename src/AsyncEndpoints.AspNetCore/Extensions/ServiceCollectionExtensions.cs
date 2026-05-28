using AsyncEndpoints.Abstractions.Infrastructure;
using AsyncEndpoints.AspNetCore.Configuration;
using AsyncEndpoints.AspNetCore.Handlers;
using AsyncEndpoints.AspNetCore.Serialization;
using AsyncEndpoints.Core.Configuration;
using AsyncEndpoints.Core.DependencyInjection;
using AsyncEndpoints.Core.Infrastructure.Serialization;
using AsyncEndpoints.Core.Legacy.Handlers;
using AsyncEndpoints.Core.Legacy.JobProcessing;
using AsyncEndpoints.Core.Legacy.Observability;
using AsyncEndpoints.Core.Legacy.Utilities;
using AsyncEndpoints.Provider.InMemory.DependencyInjection;
using AsyncEndpoints.Worker.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

namespace AsyncEndpoints.AspNetCore.Extensions;

/// <summary>
/// Extension methods for configuring and registering AsyncEndpoints services with the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Adds the core AsyncEndpoints services to the dependency injection container.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
	/// <param name="configureOptions">Optional action to configure AsyncEndpoints options.</param>
	/// <returns>The <see cref="IServiceCollection"/> for method chaining.</returns>
	public static IServiceCollection AddAsyncEndpoints(this IServiceCollection services, Action<AsyncEndpointsOptionsBuilder>? configureOptions = null)
	{
		services.AddAsyncEndpointsCore(configureOptions);

		services.AddHttpContextAccessor();
		services.AddSingleton<AsyncEndpointsResponseConfigurations>();
		services.AddScoped<IJobManager, JobManager>();
		services.AddScoped<IAsyncEndpointRequestDelegate, AsyncEndpointRequestDelegate>();
		services.AddScoped<IJsonBodyParserService, JsonBodyParserService>();
		services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
		services.TryAddSingleton<ISerializer, Serializer>();
		services.AddSingleton<IAsyncEndpointsObservability, AsyncEndpointsObservability>();
		services.AddAsyncEndpointsJsonTypeInfoResolver(AsyncEndpointsJsonSerializationContext.Default);

		return services;
	}

	/// <summary>
	/// Adds an in-memory job store implementation to the dependency injection container.
	/// Use this for development or single-instance deployments.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
	/// <returns>The <see cref="IServiceCollection"/> for method chaining.</returns>
	public static IServiceCollection AddAsyncEndpointsInMemoryStore(this IServiceCollection services)
	{
		services.AddAsyncEndpointsInMemory();
		return services;
	}

	/// <summary>
	/// Adds a JSON type information resolver to handle serialization/deserialization of AsyncEndpoints types.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
	/// <param name="jsonTypeInfoResolver">The JSON type information resolver to use.</param>
	/// <returns>The <see cref="IServiceCollection"/> for method chaining.</returns>
	public static IServiceCollection AddAsyncEndpointsJsonTypeInfoResolver(this IServiceCollection services, IJsonTypeInfoResolver jsonTypeInfoResolver)
	{
		services.ConfigureHttpJsonOptions(options =>
		{
			options.SerializerOptions.TypeInfoResolverChain.Add(jsonTypeInfoResolver);
		});

		return services;
	}

	/// <summary>
	/// Adds the background worker services required to process async jobs using the new polling-based worker engine.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
	/// <param name="configureWorker">Optional action to configure worker options.</param>
	/// <returns>The <see cref="IServiceCollection"/> for method chaining.</returns>
	public static IServiceCollection AddAsyncEndpointsWorker(this IServiceCollection services,
		Action<WorkerOptions>? configureWorker = null)
	{
		AsyncEndpoints.Worker.DependencyInjection.ServiceCollectionExtensions.AddAsyncEndpointsWorker(services, configureWorker);
		return services;
	}

	/// <summary>
	/// Registers an asynchronous endpoint handler for processing requests of type TRequest and returning responses of type TResponse.
	/// </summary>
	/// <typeparam name="TAsyncEndpointRequestHandler">The type of the handler that implements IAsyncEndpointRequestHandler<TRequest, TResponse>.</typeparam>
	/// <typeparam name="TRequest">The type of the request object.</typeparam>
	/// <typeparam name="TResponse">The type of the response object.</typeparam>
	/// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
	/// <param name="jobName">The unique name of the job, used to identify the specific handler.</param>
	/// <returns>The <see cref="IServiceCollection"/> for method chaining.</returns>
	public static IServiceCollection AddAsyncEndpointHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TAsyncEndpointRequestHandler, TRequest, TResponse>(this IServiceCollection services, string jobName)
		where TAsyncEndpointRequestHandler : class, IAsyncEndpointRequestHandler<TRequest, TResponse>
	{
		services.AddKeyedScoped<IAsyncEndpointRequestHandler<TRequest, TResponse>, TAsyncEndpointRequestHandler>(jobName);

		HandlerRegistrationTracker.Register<TRequest, TResponse>(jobName,
			(serviceProvider, request, job, cancellationToken) =>
			{
				var handler = serviceProvider.GetRequiredKeyedService<IAsyncEndpointRequestHandler<TRequest, TResponse>>(jobName);
				var context = AsyncContextBuilder.Build(request, job);
				return handler.HandleAsync(context, cancellationToken);
			});

		return services;
	}

	/// <summary>
	/// Adds an asynchronous endpoint handler for requests without body to the service collection.
	/// </summary>
	/// <typeparam name="TAsyncEndpointRequestHandler">The type of the handler that implements IAsyncEndpointRequestHandler<TResponse>.</typeparam>
	/// <typeparam name="TResponse">The type of the response object.</typeparam>
	/// <param name="services">The service collection to add the handler to.</param>
	/// <param name="jobName">A unique name for the async job, used for identifying the handler.</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddAsyncEndpointHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TAsyncEndpointRequestHandler, TResponse>(
		this IServiceCollection services,
		string jobName)
		where TAsyncEndpointRequestHandler : class, IAsyncEndpointRequestHandler<TResponse>
	{
		services.AddKeyedScoped<IAsyncEndpointRequestHandler<TResponse>, TAsyncEndpointRequestHandler>(jobName);

		HandlerRegistrationTracker.Register<NoBodyRequest, TResponse>(jobName,
			(serviceProvider, request, job, cancellationToken) =>
			{
				var handler = serviceProvider.GetRequiredKeyedService<IAsyncEndpointRequestHandler<TResponse>>(jobName);
				var genericContext = AsyncContextBuilder.Build(request, job);
				return handler.HandleAsync(genericContext, cancellationToken);
			});

		return services;
	}
}
