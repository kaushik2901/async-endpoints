using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Listener;
using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.Core.Configuration;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.Core.Listener;

public sealed class PollingJobListener : IJobListener
{
	private readonly IJobStore _store;
	private readonly AsyncEndpointsOptions _options;
	private TimeSpan _currentInterval;

	public PollingJobListener(IJobStore store, IOptions<AsyncEndpointsOptions> options)
	{
		_store = store;
		_options = options.Value;
		_currentInterval = _options.PollingMinInterval;
	}

	public async Task<JobRecord?> WaitForNextJobAsync(string channel, IReadOnlySet<int>? partitions, CancellationToken ct = default)
	{
		var job = await _store.DequeueAsync(channel, partitions, ct);

		if (job is not null)
		{
			_currentInterval = _options.PollingMinInterval;
		}
		else
		{
			var doubled = TimeSpan.FromMilliseconds(_currentInterval.TotalMilliseconds * 2);
			_currentInterval = doubled > _options.PollingMaxInterval ? _options.PollingMaxInterval : doubled;
		}

		await Task.Delay(_currentInterval, ct);

		return job;
	}
}
