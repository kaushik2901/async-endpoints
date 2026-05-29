using AsyncEndpoints.AspNetCore.Configuration;
using AsyncEndpoints.AspNetCore.Extensions;
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
		Assert.NotNull(provider.GetService<AsyncEndpointsResponseConfigurations>());
	}
}
