using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Listener;
using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.Core.Execution;
using AsyncEndpoints.Worker.Concurrency;
using AsyncEndpoints.Worker.Execution;
using AsyncEndpoints.Worker.Heartbeat;
using AsyncEndpoints.Worker.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AsyncEndpoints.Worker.UnitTests;

public class JobWorkerServiceTests
{
	[Fact]
	public async Task ExecuteAsync_LoopsUntilCancelled()
	{
		var mockListener = new Mock<IJobListener>();
		mockListener.Setup(l => l.WaitForNextJobAsync("default", null, It.IsAny<CancellationToken>()))
			.ReturnsAsync((JobRecord?)null);

		var mockStore = new Mock<IJobStore>();
		var options = Options.Create(new WorkerOptions { DefaultChannel = "default", HeartbeatInterval = TimeSpan.FromMilliseconds(100) });

		var worker = CreateWorker(mockListener.Object, mockStore.Object, options);

		using var cts = new CancellationTokenSource(200);

		await worker.StartAsync(cts.Token);
		await worker.StopAsync(CancellationToken.None);

		mockListener.Verify(l => l.WaitForNextJobAsync("default", null, It.IsAny<CancellationToken>()),
			Times.AtLeast(1));
	}

	[Fact]
	public async Task ExecuteAsync_ProcessesJob_WhenDequeued()
	{
		var jobId = Guid.NewGuid();
		var record = new JobRecord { JobId = jobId, JobName = "TestJob" };

		var mockListener = new Mock<IJobListener>();
		mockListener.SetupSequence(l => l.WaitForNextJobAsync("default", null, It.IsAny<CancellationToken>()))
			.ReturnsAsync(record)
			.ReturnsAsync((JobRecord?)null);

		var mockStore = new Mock<IJobStore>();
		var options = Options.Create(new WorkerOptions { DefaultChannel = "default", HeartbeatInterval = TimeSpan.FromMilliseconds(50) });

		var worker = CreateWorker(mockListener.Object, mockStore.Object, options);

		using var cts = new CancellationTokenSource(500);

		await worker.StartAsync(cts.Token);
		await Task.Delay(300);
		await worker.StopAsync(CancellationToken.None);

		mockStore.Verify(s => s.UpdateStatusAsync(jobId, JobStatus.Completed, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task ExecuteAsync_Backpressure_WhenConcurrencyFull()
	{
		var job1 = new JobRecord { JobId = Guid.NewGuid(), JobName = "Job1" };
		var job2 = new JobRecord { JobId = Guid.NewGuid(), JobName = "Job2" };

		var mockListener = new Mock<IJobListener>();
		mockListener.SetupSequence(l => l.WaitForNextJobAsync("default", null, It.IsAny<CancellationToken>()))
			.ReturnsAsync(job1)
			.ReturnsAsync(job2)
			.ReturnsAsync((JobRecord?)null);

		var mockStore = new Mock<IJobStore>();
		var options = Options.Create(new WorkerOptions
		{
			DefaultChannel = "default",
			MaxConcurrency = 1,
			HeartbeatInterval = TimeSpan.FromMilliseconds(200)
		});
		var concurrencyOptions = Options.Create(new WorkerOptions { MaxConcurrency = 1 });

		var handlerRegistry = new Mock<IHandlerRegistry>();
		handlerRegistry.Setup(r => r.GetInvoker(It.IsAny<string>()))
			.Returns<string>((jobName) =>
			{
				return async (sp, record, ct) =>
				{
					await Task.Delay(500, ct);
				};
			});

		var dispatcher = new JobDispatcher(
			Mock.Of<IServiceProvider>(),
			handlerRegistry.Object,
			Mock.Of<ILogger<JobDispatcher>>());

		var retryHandler = new RetryHandler(options);
		var pipeline = new JobExecutionPipeline(
			dispatcher,
			mockStore.Object,
			retryHandler,
			options,
			Mock.Of<ILogger<JobExecutionPipeline>>());

		var concurrencyManager = new WorkerConcurrencyManager(concurrencyOptions);
		var heartbeatService = new HeartbeatService(
			mockStore.Object,
			Options.Create(new WorkerOptions { HeartbeatInterval = TimeSpan.FromMilliseconds(200) }),
			Mock.Of<ILogger<HeartbeatService>>());

		var worker = new JobWorkerService(
			"default",
			mockListener.Object,
			pipeline,
			concurrencyManager,
			heartbeatService,
			options,
			Mock.Of<ILogger<JobWorkerService>>());

		using var cts = new CancellationTokenSource(3000);

		await worker.StartAsync(cts.Token);
		await Task.Delay(1200);
		await worker.StopAsync(CancellationToken.None);

		mockStore.Verify(s => s.UpdateStatusAsync(job2.JobId, It.IsAny<JobStatus>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.AtLeast(1));
	}

	private static JobWorkerService CreateWorker(
		IJobListener listener,
		IJobStore store,
		IOptions<WorkerOptions> options)
	{
		var handlerRegistry = new Mock<IHandlerRegistry>();
		handlerRegistry.Setup(r => r.GetInvoker(It.IsAny<string>()))
			.Returns<string>((jobName) => (sp, record, ct) => Task.CompletedTask);

		var dispatcher = new JobDispatcher(
			Mock.Of<IServiceProvider>(),
			handlerRegistry.Object,
			Mock.Of<ILogger<JobDispatcher>>());

		var retryHandler = new RetryHandler(options);
		var pipeline = new JobExecutionPipeline(
			dispatcher,
			store,
			retryHandler,
			options,
			Mock.Of<ILogger<JobExecutionPipeline>>());

		var concurrencyManager = new WorkerConcurrencyManager(options);
		var heartbeatService = new HeartbeatService(
			store,
			options,
			Mock.Of<ILogger<HeartbeatService>>());

		return new JobWorkerService(
			"default",
			listener,
			pipeline,
			concurrencyManager,
			heartbeatService,
			options,
			Mock.Of<ILogger<JobWorkerService>>());
	}
}
