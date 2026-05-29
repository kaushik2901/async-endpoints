using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.Worker.Heartbeat;

public sealed class HeartbeatService
{
	private readonly IJobStore _store;
	private readonly AsyncEndpointsOptions _options;
	private readonly ILogger<HeartbeatService> _logger;

	public HeartbeatService(IJobStore store, IOptions<AsyncEndpointsOptions> options, ILogger<HeartbeatService> logger)
	{
		_store = store;
		_options = options.Value;
		_logger = logger;
	}

	public async Task<IAsyncDisposable> StartHeartbeat(Guid jobId, CancellationToken ct)
	{
		var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		var heartbeatTask = RunHeartbeatLoopAsync(jobId, cts.Token);

		return new HeartbeatSubscription(heartbeatTask, cts);
	}

	private async Task RunHeartbeatLoopAsync(Guid jobId, CancellationToken ct)
	{
		try
		{
			while (!ct.IsCancellationRequested)
			{
				await Task.Delay(_options.HeartbeatInterval, ct);
				await _store.HeartbeatAsync(jobId, ct);
				_logger.LogTrace("Heartbeat sent for job {JobId}", jobId);
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Heartbeat loop failed for job {JobId}", jobId);
		}
	}

	private sealed class HeartbeatSubscription : IAsyncDisposable
	{
		private readonly Task _heartbeatTask;
		private readonly CancellationTokenSource _cts;

		public HeartbeatSubscription(Task heartbeatTask, CancellationTokenSource cts)
		{
			_heartbeatTask = heartbeatTask;
			_cts = cts;
		}

		public async ValueTask DisposeAsync()
		{
			await _cts.CancelAsync();
			try
			{
				await _heartbeatTask;
			}
			catch (OperationCanceledException)
			{
			}
			_cts.Dispose();
		}
	}
}
