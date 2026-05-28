using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.Core.Execution;
using AsyncEndpoints.Worker.Execution;
using AsyncEndpoints.Worker.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AsyncEndpoints.Worker.UnitTests;

public class JobExecutionPipelineTests
{
	private readonly Mock<IJobStore> _mockStore;
	private readonly RetryHandler _retryHandler;
	private readonly ILogger<JobExecutionPipeline> _logger;
	private readonly WorkerOptions _options;

	public JobExecutionPipelineTests()
	{
		_mockStore = new Mock<IJobStore>();
		_options = new WorkerOptions { MaxRetries = 3 };
		_retryHandler = new RetryHandler(Options.Create(_options));
		_logger = Mock.Of<ILogger<JobExecutionPipeline>>();
	}

	private JobExecutionPipeline CreatePipeline(JobDispatcher dispatcher)
	{
		return new JobExecutionPipeline(
			dispatcher,
			_mockStore.Object,
			_retryHandler,
			Options.Create(_options),
			_logger);
	}

	private JobDispatcher CreateDispatcherWithHandler(bool succeeds, string? errorMessage = null)
	{
		var handlerRegistry = new Mock<IHandlerRegistry>();
		handlerRegistry.Setup(r => r.GetInvoker(It.IsAny<string>()))
			.Returns<string>((jobName) =>
			{
				return (sp, record, ct) =>
				{
					if (succeeds)
						return Task.CompletedTask;
					throw new InvalidOperationException(errorMessage ?? "handler error");
				};
			});

		return new JobDispatcher(
			Mock.Of<IServiceProvider>(),
			handlerRegistry.Object,
			Mock.Of<ILogger<JobDispatcher>>());
	}

	private JobDispatcher CreateDispatcherWithNoHandler()
	{
		var handlerRegistry = new Mock<IHandlerRegistry>();
		handlerRegistry.Setup(r => r.GetInvoker(It.IsAny<string>()))
			.Returns((Func<IServiceProvider, JobRecord, CancellationToken, Task>?)null);

		return new JobDispatcher(
			Mock.Of<IServiceProvider>(),
			handlerRegistry.Object,
			Mock.Of<ILogger<JobDispatcher>>());
	}

	[Fact]
	public async Task ExecuteAsync_Success_UpdatesStatusToCompleted()
	{
		var jobId = Guid.NewGuid();
		var record = new JobRecord { JobId = jobId, JobName = "TestJob", MaxRetries = 3 };
		var dispatcher = CreateDispatcherWithHandler(true);
		var pipeline = CreatePipeline(dispatcher);

		await pipeline.ExecuteAsync(record, default);

		_mockStore.Verify(s => s.UpdateStatusAsync(jobId, JobStatus.Completed, null, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task ExecuteAsync_NoHandler_ReportsFailure()
	{
		var jobId = Guid.NewGuid();
		var record = new JobRecord { JobId = jobId, JobName = "UnknownJob", RetryCount = 0, MaxRetries = 3 };
		var dispatcher = CreateDispatcherWithNoHandler();
		var pipeline = CreatePipeline(dispatcher);

		await pipeline.ExecuteAsync(record, default);

		_mockStore.Verify(s => s.UpdateStatusAsync(jobId, JobStatus.Queued, It.Is<string?>(m => m != null && m.Contains("No handler registered")), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task ExecuteAsync_RetryableFailure_RequeuesJob()
	{
		var jobId = Guid.NewGuid();
		var record = new JobRecord { JobId = jobId, JobName = "TestJob", RetryCount = 0, MaxRetries = 3 };
		var dispatcher = CreateDispatcherWithHandler(false, "handler error");
		var pipeline = CreatePipeline(dispatcher);

		await pipeline.ExecuteAsync(record, default);

		_mockStore.Verify(s => s.UpdateStatusAsync(jobId, JobStatus.Queued, "handler error", It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task ExecuteAsync_NonRetryableFailure_DeadLetters()
	{
		var jobId = Guid.NewGuid();
		var record = new JobRecord { JobId = jobId, JobName = "TestJob", RetryCount = 3, MaxRetries = 3 };
		var dispatcher = CreateDispatcherWithHandler(false, "final error");
		var pipeline = CreatePipeline(dispatcher);

		await pipeline.ExecuteAsync(record, default);

		_mockStore.Verify(s => s.UpdateStatusAsync(jobId, JobStatus.DeadLettered, "final error", It.IsAny<CancellationToken>()), Times.Once);
	}
}
