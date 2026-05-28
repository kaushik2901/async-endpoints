using AsyncEndpoints.AspNetCore.Configuration;
using AsyncEndpoints.AspNetCore.Extensions;
using AsyncEndpoints.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncEndpoints.AspNetCore.UnitTests.Extensions;

public class ServiceCollectionExtensionsTests
{
	[Fact]
	public void AddAsyncEndpointsAspNetCore_RegistersAspNetCoreServices()
	{
		var services = new ServiceCollection();
		services.AddLogging();

		services.AddAsyncEndpointsAspNetCore();

		var provider = services.BuildServiceProvider();

		Assert.NotNull(provider.GetService<IHttpContextAccessor>());
		Assert.NotNull(provider.GetService<AspNetCoreOptions>());
	}

	[Fact]
	public void AddAsyncEndpoints_RegistersCoreAndAspNetCoreServices()
	{
		var services = new ServiceCollection();
		services.AddLogging();

		services.AddAsyncEndpoints();

		var provider = services.BuildServiceProvider();

		Assert.NotNull(provider.GetService<IHttpContextAccessor>());
		Assert.NotNull(provider.GetService<AspNetCoreOptions>());
		Assert.NotNull(provider.GetService<AsyncEndpointsOptions>());
	}

	[Fact]
	public void AddAsyncEndpoints_ConfiguresWithOptions()
	{
		var services = new ServiceCollection();
		services.AddLogging();

		services.AddAsyncEndpoints(options => options.WithMaxConcurrency(8).WithMaxRetries(5));
		var provider = services.BuildServiceProvider();
		var config = provider.GetRequiredService<AsyncEndpointsOptions>();

		Assert.Equal(8, config.MaxConcurrency);
		Assert.Equal(5, config.MaxRetries);
	}
}
