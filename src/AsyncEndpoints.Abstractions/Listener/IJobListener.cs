using AsyncEndpoints.Abstractions.Jobs;

namespace AsyncEndpoints.Abstractions.Listener;

/// <summary>
/// Defines the contract for listening and waiting for new jobs.
/// </summary>
public interface IJobListener
{
	/// <summary>
	/// Waits for the next available job in the specified channel.
	/// </summary>
	/// <param name="channel">The channel to listen on.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The next job record, or null if no job is available within the wait period.</returns>
	Task<JobRecord?> WaitForNextJobAsync(string channel, CancellationToken ct);
}
