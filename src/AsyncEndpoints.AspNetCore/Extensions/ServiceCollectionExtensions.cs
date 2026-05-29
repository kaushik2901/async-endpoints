using AsyncEndpoints.AspNetCore.Configuration;
using AsyncEndpoints.AspNetCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json.Serialization;

namespace AsyncEndpoints.AspNetCore.Extensions;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddAsyncEndpointsAspNetCore(this IServiceCollection services)
	{
		services.AddHttpContextAccessor();
		services.AddSingleton<AspNetCoreOptions>();
		services.AddSingleton<AsyncEndpointsResponseConfigurations>();
		services.TryAddSingleton<AspNetCoreJsonContext>();
		services.TryAddSingleton<JsonSerializerContext>(sp => sp.GetRequiredService<AspNetCoreJsonContext>());
		return services;
	}
}
