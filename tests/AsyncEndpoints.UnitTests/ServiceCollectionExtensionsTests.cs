using AsyncEndpoints.Abstractions.Infrastructure;
using AsyncEndpoints.AspNetCore.Extensions;
using AsyncEndpoints.AspNetCore.Handlers;
using AsyncEndpoints.Core.Configuration;
using AsyncEndpoints.Core.Handlers;
using AsyncEndpoints.Core.JobProcessing;
using AsyncEndpoints.Provider.InMemory.JobProcessing;
using AsyncEndpoints.UnitTests.TestSupport;
using AsyncEndpoints.Worker.Concurrency;
using AsyncEndpoints.Worker.Execution;
using AsyncEndpoints.Worker.Heartbeat;
using AsyncEndpoints.Worker.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

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
		services.AddSingleton(Mock.Of<IJobStore>());
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
		services.AddSingleton(Mock.Of<IJobStore>());
		services.AddAsyncEndpoints();

		// Act
		services.AddAsyncEndpointsInMemoryStore();

		// Assert
		var provider = services.BuildServiceProvider();
		var jobStore = provider.GetService<Abstractions.Storage.IJobStore>();

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
		services.AddSingleton(Mock.Of<IJobStore>());
		services.AddAsyncEndpointsInMemoryStore();
		services.AddAsyncEndpoints();

		// Act
		services.AddAsyncEndpointsWorker();

		// Assert
		var provider = services.BuildServiceProvider();

		Assert.NotNull(provider.GetService<WorkerConcurrencyManager>());
		Assert.NotNull(provider.GetService<HeartbeatService>());
		Assert.NotNull(provider.GetService<RetryHandler>());
		Assert.NotNull(provider.GetService<JobExecutionPipeline>());
		Assert.NotNull(provider.GetService<IJobManager>());

		Assert.IsType<WorkerConcurrencyManager>(provider.GetService<WorkerConcurrencyManager>());
		Assert.IsType<HeartbeatService>(provider.GetService<HeartbeatService>());
		Assert.IsType<RetryHandler>(provider.GetService<RetryHandler>());
		Assert.IsType<JobExecutionPipeline>(provider.GetService<JobExecutionPipeline>());
		Assert.IsType<JobManager>(provider.GetService<IJobManager>());

		var hostedServices = provider.GetServices<IHostedService>();
		Assert.Contains(hostedServices, s => s is JobWorkerService);
		Assert.Contains(hostedServices, s => s is StaleJobSweeper);
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
