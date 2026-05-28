using AsyncEndpoints.AspNetCore.Configuration;
using AsyncEndpoints.Core.Configuration;
using AsyncEndpoints.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Serialization.Metadata;

namespace AsyncEndpoints.AspNetCore.Extensions;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddAsyncEndpointsAspNetCore(this IServiceCollection services)
	{
		services.AddHttpContextAccessor();
		services.AddSingleton<AspNetCoreOptions>();
		return services;
	}

	[Obsolete("Use AddAsyncEndpointsCore + AddAsyncEndpointsAspNetCore instead.")]
	public static IServiceCollection AddAsyncEndpoints(this IServiceCollection services, Action<AsyncEndpointsOptionsBuilder>? configureOptions = null)
	{
		services.AddAsyncEndpointsCore(configureOptions);
		services.AddAsyncEndpointsAspNetCore();
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
}
