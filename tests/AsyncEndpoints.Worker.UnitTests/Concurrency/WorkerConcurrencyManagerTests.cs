using AsyncEndpoints.Worker.Concurrency;

namespace AsyncEndpoints.Worker.UnitTests.Concurrency;

public class WorkerConcurrencyManagerTests
{
	[Fact]
	public async Task ConcurrencyLimit_ShouldBeEnforced()
	{
		// Arrange
		var manager = new WorkerConcurrencyManager(2);

		// Act
		await manager.WaitAsync(CancellationToken.None);
		await manager.WaitAsync(CancellationToken.None);

		// Assert
		Assert.Equal(0, manager.AvailableSlots);

		var waitTask = manager.WaitAsync(new CancellationTokenSource(100).Token);
		await Assert.ThrowsAsync<OperationCanceledException>(() => waitTask);
	}

	[Fact]
	public async Task ReleaseSlot_ShouldIncreaseAvailableSlots()
	{
		// Arrange
		var manager = new WorkerConcurrencyManager(2);
		await manager.WaitAsync(CancellationToken.None);

		// Act
		manager.ReleaseSlot();

		// Assert
		Assert.Equal(2, manager.AvailableSlots);
	}

	[Fact]
	public async Task Register_And_ReleaseJob_ShouldTrackActiveJobs()
	{
		// Arrange
		var manager = new WorkerConcurrencyManager(2);
		var jobId = Guid.NewGuid();
		await manager.WaitAsync(CancellationToken.None);

		// Act
		manager.RegisterJob(jobId);

		// Assert
		Assert.Contains(jobId, manager.GetActiveJobIds());

		// Act
		manager.ReleaseJob(jobId);

		// Assert
		Assert.Empty(manager.GetActiveJobIds());
	}
}
