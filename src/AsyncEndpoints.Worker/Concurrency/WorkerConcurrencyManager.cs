using AsyncEndpoints.Core.Configuration;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace AsyncEndpoints.Worker.Concurrency;

public sealed class WorkerConcurrencyManager : IDisposable
{
	private readonly ConcurrentDictionary<string, SemaphoreSlim> _channelSemaphores = new(StringComparer.OrdinalIgnoreCase);
	private readonly ConcurrentDictionary<(string channel, int partition), SemaphoreSlim> _partitionSemaphores = new();
	private readonly AsyncEndpointsOptions _options;
	private bool _disposed;

	public WorkerConcurrencyManager(IOptions<AsyncEndpointsOptions> options)
	{
		_options = options.Value;
	}

	public async Task WaitAsync(string channel, int? partition, CancellationToken ct)
	{
		var channelSemaphore = _channelSemaphores.GetOrAdd(
			channel,
			_ => new SemaphoreSlim(_options.MaxConcurrency, _options.MaxConcurrency));

		await channelSemaphore.WaitAsync(ct);

		if (partition.HasValue)
		{
			var partitionSemaphore = _partitionSemaphores.GetOrAdd(
				(channel, partition.Value),
				_ => new SemaphoreSlim(1, 1));

			await partitionSemaphore.WaitAsync(ct);
		}
	}

	public void Release(string channel, int? partition)
	{
		if (_channelSemaphores.TryGetValue(channel, out var channelSemaphore))
		{
			channelSemaphore.Release();
		}

		if (partition.HasValue)
		{
			if (_partitionSemaphores.TryGetValue((channel, partition.Value), out var partitionSemaphore))
			{
				partitionSemaphore.Release();
			}
		}
	}

	public int CurrentCount(string channel)
	{
		if (_channelSemaphores.TryGetValue(channel, out var semaphore))
		{
			return semaphore.CurrentCount;
		}
		return _options.MaxConcurrency;
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;

		foreach (var semaphore in _channelSemaphores.Values)
		{
			semaphore.Dispose();
		}

		foreach (var semaphore in _partitionSemaphores.Values)
		{
			semaphore.Dispose();
		}

		_channelSemaphores.Clear();
		_partitionSemaphores.Clear();
	}
}
