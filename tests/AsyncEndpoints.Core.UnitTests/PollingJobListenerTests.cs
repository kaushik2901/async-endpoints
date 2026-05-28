using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.Core.Configuration;
using AsyncEndpoints.Core.Listener;
using Microsoft.Extensions.Options;
using Moq;

namespace AsyncEndpoints.Core.UnitTests;

public class PollingJobListenerTests
{
	[Fact]
	public async Task WaitForNextJobAsync_CallsStoreDequeue()
	{
		var mockStore = new Mock<IJobStore>();
		var options = new AsyncEndpointsOptions();
		var listener = new PollingJobListener(mockStore.Object, Options.Create(options));

		mockStore.Setup(s => s.DequeueAsync("default", null, It.IsAny<CancellationToken>()))
			.ReturnsAsync((JobRecord?)null);

		var result = await listener.WaitForNextJobAsync("default", null, CancellationToken.None);

		mockStore.Verify(s => s.DequeueAsync("default", null, It.IsAny<CancellationToken>()), Times.Once);
		Assert.Null(result);
	}

	[Fact]
	public async Task WaitForNextJobAsync_AdaptiveBackoff_ResetsOnSuccess()
	{
		var mockStore = new Mock<IJobStore>();
		var options = new AsyncEndpointsOptions
		{
			PollingMinInterval = TimeSpan.FromMilliseconds(10),
			PollingMaxInterval = TimeSpan.FromSeconds(30)
		};
		var listener = new PollingJobListener(mockStore.Object, Options.Create(options));

		var jobRecord = new JobRecord { JobId = Guid.NewGuid(), JobName = "test" };

		mockStore.SetupSequence(s => s.DequeueAsync("default", null, It.IsAny<CancellationToken>()))
			.ReturnsAsync((JobRecord?)null)
			.ReturnsAsync((JobRecord?)null)
			.ReturnsAsync(jobRecord);

		var result1 = await listener.WaitForNextJobAsync("default", null, CancellationToken.None);
		Assert.Null(result1);

		var result2 = await listener.WaitForNextJobAsync("default", null, CancellationToken.None);
		Assert.Null(result2);

		var result3 = await listener.WaitForNextJobAsync("default", null, CancellationToken.None);
		Assert.NotNull(result3);
		Assert.Equal(jobRecord.JobId, result3.JobId);

		mockStore.Verify(s => s.DequeueAsync("default", null, It.IsAny<CancellationToken>()), Times.Exactly(3));
	}

	[Fact]
	public async Task WaitForNextJobAsync_AdaptiveBackoff_DoublesOnEmpty()
	{
		var mockStore = new Mock<IJobStore>();
		var options = new AsyncEndpointsOptions
		{
			PollingMinInterval = TimeSpan.FromMilliseconds(50),
			PollingMaxInterval = TimeSpan.FromMilliseconds(500)
		};
		var listener = new PollingJobListener(mockStore.Object, Options.Create(options));

		mockStore.Setup(s => s.DequeueAsync("default", null, It.IsAny<CancellationToken>()))
			.ReturnsAsync((JobRecord?)null);

		var sw = System.Diagnostics.Stopwatch.StartNew();
		await listener.WaitForNextJobAsync("default", null, CancellationToken.None);
		var firstElapsed = sw.Elapsed;

		sw.Restart();
		await listener.WaitForNextJobAsync("default", null, CancellationToken.None);
		var secondElapsed = sw.Elapsed;

		Assert.True(firstElapsed.TotalMilliseconds >= 40,
			$"First call took {firstElapsed.TotalMilliseconds}ms, expected >= 40ms");
		Assert.True(secondElapsed.TotalMilliseconds >= firstElapsed.TotalMilliseconds * 0.8,
			$"Second call took {secondElapsed.TotalMilliseconds}ms, expected >= {firstElapsed.TotalMilliseconds * 0.8}ms");
	}

	[Fact]
	public async Task WaitForNextJobAsync_RespectsCancellationToken()
	{
		var mockStore = new Mock<IJobStore>();
		var options = new AsyncEndpointsOptions
		{
			PollingMinInterval = TimeSpan.FromMilliseconds(5000)
		};
		var listener = new PollingJobListener(mockStore.Object, Options.Create(options));

		using var cts = new CancellationTokenSource();
		cts.Cancel();

		await Assert.ThrowsAsync<TaskCanceledException>(() =>
			listener.WaitForNextJobAsync("default", null, cts.Token));
	}
}
