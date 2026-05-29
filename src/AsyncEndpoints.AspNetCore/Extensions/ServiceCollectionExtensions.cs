using AsyncEndpoints.AspNetCore.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Serialization.Metadata;

namespace AsyncEndpoints.AspNetCore.Extensions;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddAsyncEndpointsAspNetCore(this IServiceCollection services)
	{
		services.AddHttpContextAccessor();
		services.AddSingleton<AspNetCoreOptions>();
		services.AddSingleton<AsyncEndpointsResponseConfigurations>();
		services.AddAsyncEndpointsJsonTypeInfoResolver(AsyncEndpointsAspNetCoreJsonSerializationContext.Default);
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
