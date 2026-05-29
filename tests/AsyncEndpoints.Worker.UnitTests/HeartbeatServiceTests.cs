using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.Core.Configuration;
using AsyncEndpoints.Worker.Heartbeat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AsyncEndpoints.Worker.UnitTests;

public class HeartbeatServiceTests
{
	[Fact]
	public async Task StartHeartbeat_SendsHeartbeats_OnInterval()
	{
		var jobId = Guid.NewGuid();
		var mockStore = new Mock<IJobStore>();
		var options = new AsyncEndpointsOptions { HeartbeatInterval = TimeSpan.FromMilliseconds(50) };
		var logger = Mock.Of<ILogger<HeartbeatService>>();

		var service = new HeartbeatService(mockStore.Object, Options.Create(options), logger);

		await using var heartbeat = await service.StartHeartbeat(jobId, default);

		await Task.Delay(120);

		mockStore.Verify(s => s.HeartbeatAsync(jobId, It.IsAny<CancellationToken>()), Times.AtLeast(2));
	}

	[Fact]
	public async Task StartHeartbeat_Dispose_StopsHeartbeat()
	{
		var jobId = Guid.NewGuid();
		var mockStore = new Mock<IJobStore>();
		var options = new AsyncEndpointsOptions { HeartbeatInterval = TimeSpan.FromMilliseconds(20) };
		var logger = Mock.Of<ILogger<HeartbeatService>>();

		var service = new HeartbeatService(mockStore.Object, Options.Create(options), logger);

		var heartbeat = await service.StartHeartbeat(jobId, default);

		await Task.Delay(50);

		await heartbeat.DisposeAsync();

		var countBefore = mockStore.Invocations.Count(i => i.Method.Name == nameof(IJobStore.HeartbeatAsync));

		await Task.Delay(60);

		var countAfter = mockStore.Invocations.Count(i => i.Method.Name == nameof(IJobStore.HeartbeatAsync));

		Assert.Equal(countBefore, countAfter);
	}

	[Fact]
	public async Task StartHeartbeat_StopsOnCancellationToken()
	{
		var jobId = Guid.NewGuid();
		var mockStore = new Mock<IJobStore>();
		var options = new AsyncEndpointsOptions { HeartbeatInterval = TimeSpan.FromMilliseconds(20) };
		var logger = Mock.Of<ILogger<HeartbeatService>>();

		var service = new HeartbeatService(mockStore.Object, Options.Create(options), logger);

		using var cts = new CancellationTokenSource();
		var heartbeat = await service.StartHeartbeat(jobId, cts.Token);

		await Task.Delay(50);

		cts.Cancel();
		await Task.Delay(60);

		var countBefore = mockStore.Invocations.Count(i => i.Method.Name == nameof(IJobStore.HeartbeatAsync));

		await Task.Delay(60);

		var countAfter = mockStore.Invocations.Count(i => i.Method.Name == nameof(IJobStore.HeartbeatAsync));

		Assert.Equal(countBefore, countAfter);
	}
}
