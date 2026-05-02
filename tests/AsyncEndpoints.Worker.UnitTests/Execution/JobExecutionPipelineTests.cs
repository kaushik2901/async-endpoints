using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.Core.Execution;
using AsyncEndpoints.Worker.Concurrency;
using AsyncEndpoints.Worker.Execution;
using NSubstitute;

namespace AsyncEndpoints.Worker.UnitTests.Execution;

public class JobExecutionPipelineTests
{
	[Fact]
	public async Task RunAsync_HappyPath_ShouldCompleteJob()
	{
		// Arrange
		var store = Substitute.For<IJobStore>();
		var dispatcher = Substitute.For<JobDispatcher>(null, null, null);
		var concurrency = new WorkerConcurrencyManager(10);
		var retry = new RetryHandler(store);

		var pipeline = new JobExecutionPipeline(store, dispatcher, retry, concurrency);
		var record = new JobRecord { JobId = Guid.NewGuid() };

		// Ensure DispatchAsync doesn't call real implementation which would throw NRE
		dispatcher.DispatchAsync(Arg.Any<JobRecord>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

		// Act
		await concurrency.WaitAsync(CancellationToken.None);
		await pipeline.RunAsync(record, CancellationToken.None);

		// Assert
		await store.Received(1).UpdateStatusAsync(record.JobId, JobStatus.Processing, null, null, null, null, Arg.Any<CancellationToken>());
		await dispatcher.Received(1).DispatchAsync(record, Arg.Any<CancellationToken>());
		await store.Received(1).UpdateStatusAsync(record.JobId, JobStatus.Completed, null, null, null, null, Arg.Any<CancellationToken>());
		Assert.Empty(concurrency.GetActiveJobIds());
	}

	[Fact]
	public async Task RunAsync_OnFailure_ShouldInvokeRetryHandler()
	{
		// Arrange
		var store = Substitute.For<IJobStore>();
		var dispatcher = Substitute.For<JobDispatcher>(null, null, null);
		var concurrency = new WorkerConcurrencyManager(10);
		var retry = Substitute.For<RetryHandler>(store);

		var pipeline = new JobExecutionPipeline(store, dispatcher, retry, concurrency);
		var record = new JobRecord { JobId = Guid.NewGuid() };

		// Setup
		dispatcher.DispatchAsync(Arg.Any<JobRecord>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new Exception("Execution failed")));
		retry.HandleFailureAsync(Arg.Any<JobRecord>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

		// Act
		await concurrency.WaitAsync(CancellationToken.None);
		await pipeline.RunAsync(record, CancellationToken.None);

		// Assert
		await store.Received(1).UpdateStatusAsync(
			Arg.Is(record.JobId),
			Arg.Is(JobStatus.Failed),
			Arg.Is<string?>(s => s == null),
			Arg.Is<string?>(s => s != null),
			Arg.Is<int?>(i => i == null),
			Arg.Is<DateTimeOffset?>(d => d == null),
			Arg.Any<CancellationToken>());

		await retry.Received(1).HandleFailureAsync(record, Arg.Any<Exception>(), Arg.Any<CancellationToken>());
		Assert.Empty(concurrency.GetActiveJobIds());
	}
}
