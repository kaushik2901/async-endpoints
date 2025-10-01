using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;
using AsyncEndpoints.BackgroundWorker;
using AsyncEndpoints.Contracts;
using AsyncEndpoints.InMemoryStore;
using AsyncEndpoints.Serialization;
using AsyncEndpoints.Services;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncEndpoints;

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
	public static IServiceCollection AddAsyncEndpoints(this IServiceCollection services, Action<AsyncEndpointsConfigurations>? configureOptions = null)
	{
		if (configureOptions != null)
		{
			services.Configure(configureOptions);
		}

		services.AddHttpContextAccessor();
		services.AddSingleton<AsyncEndpointsConfigurations>();
		services.AddScoped<IJobManager, JobManager>();
		services.AddScoped<IAsyncEndpointRequestDelegate, AsyncEndpointRequestDelegate>();
		services.AddScoped<IJobResponseService, JobResponseService>();
		services.AddScoped<IJsonBodyParserService, JsonBodyParserService>();
		services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
		services.AddSingleton<ISerializer, Serializer>();
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
		services.AddSingleton<IJobStore, InMemoryJobStore>();

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
	/// Adds the background worker services required to process async jobs.
	/// This includes job consumers, producers, processors, and the hosted background service.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
	/// <returns>The <see cref="IServiceCollection"/> for method chaining.</returns>
	public static IServiceCollection AddAsyncEndpointsWorker(this IServiceCollection services)
	{
		services.AddTransient<IJobConsumerService, JobConsumerService>();
		services.AddTransient<IJobProducerService, JobProducerService>();
		services.AddTransient<IJobProcessorService, JobProcessorService>();
		services.AddTransient<IHandlerExecutionService, HandlerExecutionService>();
		services.AddHostedService<AsyncEndpointsBackgroundService>();

		return services;
	}

	/// <summary>
	/// Registers an asynchronous endpoint handler for processing requests of type TRequest and returning responses of type TResponse.
	/// </summary>
	/// <typeparam name="TAsyncEndpointRequestHandler">The type of the handler that implements IAsyncEndpointRequestHandler.</typeparam>
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
			async (serviceProvider, request, job, cancellationToken) =>
			{
				var handler = serviceProvider.GetRequiredKeyedService<IAsyncEndpointRequestHandler<TRequest, TResponse>>(jobName);
				var context = AsyncContextBuilder.Build(request, job);
				return await handler.HandleAsync(context, cancellationToken);
			});

		return services;
	}
}