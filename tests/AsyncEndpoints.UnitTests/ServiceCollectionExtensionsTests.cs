using AsyncEndpoints.Background;
using AsyncEndpoints.Configuration;
using AsyncEndpoints.Extensions;
using AsyncEndpoints.Handlers;
using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.UnitTests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AsyncEndpoints.UnitTests;

public class ServiceCollectionExtensionsTests
{
	[Fact]
	public void AddAsyncEndpoints_RegistersServicesCorrectly()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
		services.AddAsyncEndpointsInMemoryStore();

		// Act
		services.AddAsyncEndpoints();

		// Assert
		var provider = services.BuildServiceProvider();

		Assert.NotNull(provider.GetService<IHttpContextAccessor>());
		Assert.NotNull(provider.GetService<AsyncEndpointsOptions>());
		Assert.NotNull(provider.GetService<IAsyncEndpointRequestDelegate>());

		var requestDelegate = provider.GetService<IAsyncEndpointRequestDelegate>();
		Assert.IsType<AsyncEndpointRequestDelegate>(requestDelegate);
	}

	[Fact]
	public void AddAsyncEndpoints_ConfiguresWithOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
		services.AddAsyncEndpointsInMemoryStore();

		// Act
		services.AddAsyncEndpoints(options => options.WithMaxConcurrency(8).WithMaxRetries(5));
		var provider = services.BuildServiceProvider();
		var config = provider.GetRequiredService<AsyncEndpointsOptions>();

		// Assert
		Assert.Equal(8, config.MaxConcurrency);
		Assert.Equal(5, config.MaxRetries);
	}

	[Fact]
	public void AddAsyncEndpointsInMemoryStore_RegistersInMemoryJobStore()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
		services.AddAsyncEndpoints();

		// Act
		services.AddAsyncEndpointsInMemoryStore();

		// Assert
		var provider = services.BuildServiceProvider();
		var jobStore = provider.GetService<IJobStore>();

		Assert.NotNull(jobStore);
		Assert.IsType<InMemoryJobStore>(jobStore);
	}

	[Fact]
	public void AddAsyncEndpointsWorker_RegistersWorkerServices()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
		services.AddAsyncEndpointsInMemoryStore();
		services.AddAsyncEndpoints();

		// Act
		services.AddAsyncEndpointsWorker();

		// Assert
		var provider = services.BuildServiceProvider();

		Assert.NotNull(provider.GetService<IJobConsumerService>());
		Assert.NotNull(provider.GetService<IJobProducerService>());
		Assert.NotNull(provider.GetService<IJobProcessorService>());
		Assert.NotNull(provider.GetService<IHandlerExecutionService>());
		Assert.NotNull(provider.GetService<IJobManager>());

		Assert.IsType<JobConsumerService>(provider.GetService<IJobConsumerService>());
		Assert.IsType<JobProducerService>(provider.GetService<IJobProducerService>());
		Assert.IsType<JobProcessorService>(provider.GetService<IJobProcessorService>());
		Assert.IsType<HandlerExecutionService>(provider.GetService<IHandlerExecutionService>());
		Assert.IsType<JobManager>(provider.GetService<IJobManager>());

		var hostedServices = provider.GetServices<IHostedService>();
		var backgroundService = hostedServices.FirstOrDefault(s => s is AsyncEndpointsBackgroundService);
		Assert.NotNull(backgroundService);
	}

	[Fact]
	public void AddAsyncEndpointHandler_RegistersHandlerCorrectly()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
		services.AddAsyncEndpointsInMemoryStore();

		// Act
		services.AddAsyncEndpointHandler<TestAsyncEndpointRequestHandler, TestRequest, TestResponse>("test-job");

		// Assert
		var provider = services.BuildServiceProvider();
		var handler = provider.GetKeyedService<IAsyncEndpointRequestHandler<TestRequest, TestResponse>>("test-job");

		Assert.NotNull(handler);
		Assert.IsType<TestAsyncEndpointRequestHandler>(handler);
	}

	[Fact]
	public void AddAsyncEndpointHandler_NoBody_RegistersHandlerCorrectly()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
		services.AddAsyncEndpointsInMemoryStore();

		// Act
		services.AddAsyncEndpointHandler<TestNoBodyRequestHandler, string>("no-body-test-job");

		// Assert
		var provider = services.BuildServiceProvider();
		var handler = provider.GetKeyedService<IAsyncEndpointRequestHandler<string>>("no-body-test-job");

		Assert.NotNull(handler);
		Assert.IsType<TestNoBodyRequestHandler>(handler);
	}
}
