using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Core.Execution;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.Core.UnitTests;

public class JobDispatcherTests
{
	[Fact]
	public async Task DispatchAsync_LooksUpInvoker_FromRegistry()
	{
		var handlerRegistry = new HandlerRegistry();
		var logger = Mock.Of<ILogger<JobDispatcher>>();
		var serviceProvider = Mock.Of<IServiceProvider>();
		var invoked = false;

		handlerRegistry.Register<string>("test-job", (sp, record, ct) =>
		{
			invoked = true;
			return Task.CompletedTask;
		});

		var dispatcher = new JobDispatcher(serviceProvider, handlerRegistry, logger);
		var record = new JobRecord { JobId = Guid.NewGuid(), JobName = "test-job" };

		var result = await dispatcher.DispatchAsync(record);

		Assert.True(result.IsSuccess);
		Assert.True(invoked);
	}

	[Fact]
	public async Task DispatchAsync_InvokesDelegate_WithServiceProvider()
	{
		var handlerRegistry = new HandlerRegistry();
		var logger = Mock.Of<ILogger<JobDispatcher>>();
		var serviceProvider = new Mock<IServiceProvider>();
		IServiceProvider? capturedProvider = null;

		handlerRegistry.Register<string>("test-job", (sp, record, ct) =>
		{
			capturedProvider = sp;
			return Task.CompletedTask;
		});

		var dispatcher = new JobDispatcher(serviceProvider.Object, handlerRegistry, logger);
		var record = new JobRecord { JobId = Guid.NewGuid(), JobName = "test-job" };

		await dispatcher.DispatchAsync(record);

		Assert.Same(serviceProvider.Object, capturedProvider);
	}

	[Fact]
	public async Task DispatchAsync_ReturnsFailure_WhenHandlerNotFound()
	{
		var handlerRegistry = new HandlerRegistry();
		var logger = Mock.Of<ILogger<JobDispatcher>>();
		var serviceProvider = Mock.Of<IServiceProvider>();

		var dispatcher = new JobDispatcher(serviceProvider, handlerRegistry, logger);
		var record = new JobRecord { JobId = Guid.NewGuid(), JobName = "nonexistent-job" };

		var result = await dispatcher.DispatchAsync(record);

		Assert.False(result.IsSuccess);
		Assert.Contains("nonexistent-job", result.ErrorMessage);
	}

	[Fact]
	public async Task DispatchAsync_ReturnsFailure_WhenDelegateThrows()
	{
		var handlerRegistry = new HandlerRegistry();
		var logger = Mock.Of<ILogger<JobDispatcher>>();
		var serviceProvider = Mock.Of<IServiceProvider>();

		handlerRegistry.Register<string>("failing-job", (sp, record, ct) =>
		{
			throw new InvalidOperationException("Handler error");
		});

		var dispatcher = new JobDispatcher(serviceProvider, handlerRegistry, logger);
		var record = new JobRecord { JobId = Guid.NewGuid(), JobName = "failing-job" };

		var result = await dispatcher.DispatchAsync(record);

		Assert.False(result.IsSuccess);
		Assert.Contains("Handler error", result.ErrorMessage);
	}

	[Fact]
	public async Task DispatchAsync_NoReflection_NoMakeGenericMethodUsed()
	{
		var handlerRegistry = new HandlerRegistry();
		var logger = Mock.Of<ILogger<JobDispatcher>>();
		var serviceProvider = Mock.Of<IServiceProvider>();

		handlerRegistry.Register<CustomPayload>("typed-job", (sp, record, ct) =>
		{
			return Task.CompletedTask;
		});

		var dispatcher = new JobDispatcher(serviceProvider, handlerRegistry, logger);
		var record = new JobRecord { JobId = Guid.NewGuid(), JobName = "typed-job" };

		var result = await dispatcher.DispatchAsync(record);

		Assert.True(result.IsSuccess);
	}

	private sealed record CustomPayload(string Value);
}
