using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Core.Execution;

namespace AsyncEndpoints.Core.UnitTests;

public class HandlerRegistryTests
{
	[Fact]
	public void Register_StoresDelegate_ByJobName()
	{
		var registry = new HandlerRegistry();
		Func<IServiceProvider, JobRecord, CancellationToken, Task> invoker = (sp, r, ct) => Task.CompletedTask;

		registry.Register<string>("test-job", invoker);

		var retrieved = registry.GetInvoker("test-job");
		Assert.NotNull(retrieved);
		Assert.Same(invoker, retrieved);
	}

	[Fact]
	public void GetInvoker_ReturnsNull_ForUnknownJobName()
	{
		var registry = new HandlerRegistry();

		var result = registry.GetInvoker("nonexistent-job");

		Assert.Null(result);
	}

	[Fact]
	public async Task Register_IsThreadSafe()
	{
		var registry = new HandlerRegistry();
		var tasks = new List<Task>();

		for (int i = 0; i < 100; i++)
		{
			var jobName = $"job-{i}";
			tasks.Add(Task.Run(() =>
			{
				Func<IServiceProvider, JobRecord, CancellationToken, Task> invoker = (sp, r, ct) => Task.CompletedTask;
				registry.Register<string>(jobName, invoker);
			}));
		}

		await Task.WhenAll(tasks);

		for (int i = 0; i < 100; i++)
		{
			Assert.NotNull(registry.GetInvoker($"job-{i}"));
		}
	}

	[Fact]
	public async Task RegisterAndInvoke_DelegateCapturesTypeAtCompileTime()
	{
		var registry = new HandlerRegistry();
		string? capturedTypeName = null;

		registry.Register<string>("typed-job", (sp, record, ct) =>
		{
			capturedTypeName = typeof(string).Name;
			return Task.CompletedTask;
		});

		var invoker = registry.GetInvoker("typed-job");
		Assert.NotNull(invoker);

		await invoker(null!, new JobRecord(), CancellationToken.None);

		Assert.Equal("String", capturedTypeName);
	}

	[Fact]
	public async Task MultipleRegistrations_DontConflict()
	{
		var registry = new HandlerRegistry();
		var invoked1 = false;
		var invoked2 = false;

		registry.Register<string>("job-1", (sp, r, ct) =>
		{
			invoked1 = true;
			return Task.CompletedTask;
		});

		registry.Register<string>("job-2", (sp, r, ct) =>
		{
			invoked2 = true;
			return Task.CompletedTask;
		});

		var invoker1 = registry.GetInvoker("job-1");
		var invoker2 = registry.GetInvoker("job-2");

		Assert.NotNull(invoker1);
		Assert.NotNull(invoker2);
		Assert.NotSame(invoker1, invoker2);

		await invoker1(null!, new JobRecord(), CancellationToken.None);
		Assert.True(invoked1);
		Assert.False(invoked2);
	}
}
