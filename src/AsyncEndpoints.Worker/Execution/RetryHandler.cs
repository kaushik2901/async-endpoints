using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Storage;

namespace AsyncEndpoints.Worker.Execution;

/// <summary>
/// Handles job failures and schedules retries or dead-letters.
/// </summary>
public class RetryHandler
{
	private readonly IJobStore _store;

	public RetryHandler(IJobStore store)
	{
		_store = store;
	}

	/// <summary>
	/// Handles a job failure by calculating the next state and updating the store.
	/// </summary>
	public virtual async Task HandleFailureAsync(JobRecord record, Exception ex, CancellationToken ct)
	{
		var nextRetryCount = record.RetryCount + 1;

		if (nextRetryCount > record.MaxRetries)
		{
			// Max retries exceeded, move to DeadLettered
			await _store.UpdateStatusAsync(
				record.JobId,
				JobStatus.DeadLettered,
				error: ex.ToString(),
				ct: ct);
		}
		else
		{
			// Calculate exponential backoff: 2^retry seconds
			var baseDelaySeconds = Math.Pow(2, nextRetryCount);

			// Add jitter: +/- 20%
			var jitter = (Random.Shared.NextDouble() * 0.4) + 0.8;
			var delay = TimeSpan.FromSeconds(baseDelaySeconds * jitter);

			var runAfter = DateTimeOffset.UtcNow.Add(delay);

			// Move back to Queued for retry
			await _store.UpdateStatusAsync(
				record.JobId,
				JobStatus.Queued,
				error: ex.ToString(),
				retryCount: nextRetryCount,
				runAfter: runAfter,
				ct: ct);
		}
	}
}
