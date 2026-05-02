using System.Collections.Concurrent;

namespace AsyncEndpoints.Worker.Concurrency;

/// <summary>
/// Manages and enforces concurrency limits for the worker.
/// </summary>
public class WorkerConcurrencyManager
{
	private readonly SemaphoreSlim _semaphore;
	private readonly ConcurrentDictionary<Guid, string> _activeJobs = new();
	private readonly int _maxConcurrency;

	public WorkerConcurrencyManager(int maxConcurrency)
	{
		_maxConcurrency = maxConcurrency;
		_semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
	}

	/// <summary>
	/// Waits for an available slot to execute a job.
	/// </summary>
	public Task WaitAsync(CancellationToken ct) => _semaphore.WaitAsync(ct);

	/// <summary>
	/// Releases a previously acquired slot without registering a job (e.g. if no job was found).
	/// </summary>
	public void ReleaseSlot()
	{
		_semaphore.Release();
	}

	/// <summary>
	/// Registers a job as active in an already acquired slot.
	/// </summary>
	public void RegisterJob(Guid jobId)
	{
		_activeJobs.TryAdd(jobId, string.Empty);
	}

	/// <summary>
	/// Unregisters a job and releases the concurrency slot.
	/// </summary>
	public void ReleaseJob(Guid jobId)
	{
		if (_activeJobs.TryRemove(jobId, out _))
		{
			_semaphore.Release();
		}
	}

	/// <summary>
	/// Gets the IDs of all currently active jobs.
	/// </summary>
	public IEnumerable<Guid> GetActiveJobIds() => _activeJobs.Keys;

	/// <summary>
	/// Gets the number of available slots.
	/// </summary>
	public int AvailableSlots => _semaphore.CurrentCount;

	/// <summary>
	/// Gets the maximum allowed concurrency.
	/// </summary>
	public int MaxConcurrency => _maxConcurrency;
}
