using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.Worker.Execution;
using NSubstitute;

namespace AsyncEndpoints.Worker.UnitTests.Execution;

public class RetryHandlerTests
{
	[Fact]
	public async Task HandleFailureAsync_WhenRetriesLeft_ShouldScheduleRetry()
	{
		// Arrange
		var store = Substitute.For<IJobStore>();
		var handler = new RetryHandler(store);
		var record = new JobRecord
		{
			JobId = Guid.NewGuid(),
			RetryCount = 0,
			MaxRetries = 3
		};
		var ex = new Exception("Test error");

		// Act
		await handler.HandleFailureAsync(record, ex, CancellationToken.None);

		// Assert
		await store.Received(1).UpdateStatusAsync(
			Arg.Is(record.JobId),
			Arg.Is(JobStatus.Queued),
			Arg.Is<string?>(s => s == null),
			Arg.Is<string?>(s => s != null),
			Arg.Is<int?>(i => i == 1),
			Arg.Is<DateTimeOffset?>(d => d > DateTimeOffset.UtcNow),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task HandleFailureAsync_WhenMaxRetriesExceeded_ShouldDeadLetter()
	{
		// Arrange
		var store = Substitute.For<IJobStore>();
		var handler = new RetryHandler(store);
		var record = new JobRecord
		{
			JobId = Guid.NewGuid(),
			RetryCount = 3,
			MaxRetries = 3
		};
		var ex = new Exception("Test error");

		// Act
		await handler.HandleFailureAsync(record, ex, CancellationToken.None);

		// Assert
		await store.Received(1).UpdateStatusAsync(
			Arg.Is(record.JobId),
			Arg.Is(JobStatus.DeadLettered),
			Arg.Is<string?>(s => s == null),
			Arg.Is<string?>(s => s != null),
			Arg.Is<int?>(i => i == null),
			Arg.Is<DateTimeOffset?>(d => d == null),
			Arg.Any<CancellationToken>());
	}
}
