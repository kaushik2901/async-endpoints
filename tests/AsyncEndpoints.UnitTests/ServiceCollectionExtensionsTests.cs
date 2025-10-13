using AsyncEndpoints.Background;
using AsyncEndpoints.Configuration;
using AsyncEndpoints.Extensions;
using AsyncEndpoints.Handlers;
using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.UnitTests.TestSupport;
using AsyncEndpoints.UnitTests.Utilities;
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
		services.AddLogging();  // Add logging to resolve ILogger dependencies
		services.AddSingleton<IDateTimeProvider, DateTimeProvider>(); // Add datetime provider
		services.AddAsyncEndpointsInMemoryStore(); // Add job store as it's required by the request delegate

		// Act
		services.AddAsyncEndpoints();

		// Assert
		var provider = services.BuildServiceProvider();

		Assert.NotNull(provider.GetService<IHttpContextAccessor>());
		Assert.NotNull(provider.GetService<AsyncEndpointsConfigurations>());
		Assert.NotNull(provider.GetService<IAsyncEndpointRequestDelegate>());

		// Verify the correct implementation is registered
		var requestDelegate = provider.GetService<IAsyncEndpointRequestDelegate>();
		Assert.IsType<AsyncEndpointRequestDelegate>(requestDelegate);
	}

	[Fact]
	public void AddAsyncEndpoints_ConfiguresWithOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<IDateTimeProvider, DateTimeProvider>(); // Add datetime provider
		services.AddAsyncEndpointsInMemoryStore();

		// Act - Add configuration as a separate step after AddAsyncEndpoints
		services.AddAsyncEndpoints();
		var provider = services.BuildServiceProvider();
		var config = provider.GetRequiredService<AsyncEndpointsConfigurations>();

		// Test the default values
		Assert.Equal(AsyncEndpointsConstants.DefaultPollingIntervalMs, config.WorkerConfigurations.PollingIntervalMs);
	}

	[Fact]
	public void AddAsyncEndpointsInMemoryStore_RegistersInMemoryJobStore()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();  // Add logging to resolve ILogger dependencies
		services.AddSingleton<IDateTimeProvider, DateTimeProvider>(); // Add datetime provider

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
		services.AddLogging();  // Add logging to resolve ILogger dependencies
		services.AddSingleton<IDateTimeProvider, DateTimeProvider>(); // Add datetime provider
		services.AddAsyncEndpointsInMemoryStore(); // Add job store as it's required by worker services
		services.AddAsyncEndpoints(); // Add the main services including IJobManager

		// Act
		services.AddAsyncEndpointsWorker();

		// Assert
		var provider = services.BuildServiceProvider();

		Assert.NotNull(provider.GetService<IJobConsumerService>());
		Assert.NotNull(provider.GetService<IJobProducerService>());
		Assert.NotNull(provider.GetService<IJobProcessorService>());
		Assert.NotNull(provider.GetService<IHandlerExecutionService>());
		Assert.NotNull(provider.GetService<IJobManager>()); // Add this check

		// Verify the correct implementations are registered
		Assert.IsType<JobConsumerService>(provider.GetService<IJobConsumerService>());
		Assert.IsType<JobProducerService>(provider.GetService<IJobProducerService>());
		Assert.IsType<JobProcessorService>(provider.GetService<IJobProcessorService>());
		Assert.IsType<HandlerExecutionService>(provider.GetService<IHandlerExecutionService>());
		Assert.IsType<JobManager>(provider.GetService<IJobManager>()); // Add this verification

		// Verify background service is registered
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
		services.AddSingleton<IDateTimeProvider, DateTimeProvider>(); // Add datetime provider
		services.AddAsyncEndpointsInMemoryStore(); // Required for the request delegate

		// Act
		services.AddAsyncEndpointHandler<TestAsyncEndpointRequestHandler, TestRequest, TestResponse>("test-job");

		// Assert
		var provider = services.BuildServiceProvider();
		var handler = provider.GetKeyedService<IAsyncEndpointRequestHandler<TestRequest, TestResponse>>("test-job");

		Assert.NotNull(handler);
		Assert.IsType<TestAsyncEndpointRequestHandler>(handler);
	}

	/// <summary>
	/// Verifies that the AddAsyncEndpointHandler method for no-body requests registers the handler correctly.
	/// This ensures handlers without request body can be registered and resolved from the service container.
	/// </summary>
	[Fact]
	public void AddAsyncEndpointHandler_NoBody_RegistersHandlerCorrectly()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<IDateTimeProvider, DateTimeProvider>(); // Add datetime provider
		services.AddAsyncEndpointsInMemoryStore(); // Required for the request delegate

		// Act
		services.AddAsyncEndpointHandler<TestNoBodyRequestHandler, string>("no-body-test-job");

		// Assert
		var provider = services.BuildServiceProvider();
		var handler = provider.GetKeyedService<IAsyncEndpointRequestHandler<string>>("no-body-test-job");

		Assert.NotNull(handler);
		Assert.IsType<TestNoBodyRequestHandler>(handler);
	}
}
