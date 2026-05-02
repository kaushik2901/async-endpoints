namespace AsyncEndpoints.Abstractions.Listener;

/// <summary>
/// Defines the contract for job notification signaling.
/// </summary>
public interface IJobNotifier
{
	/// <summary>
	/// Notifies that a new job is available in the specified channel.
	/// </summary>
	Task NotifyJobAvailableAsync(string channel, CancellationToken ct = default);

	/// <summary>
	/// Waits until a job is available in the specified channel, up to a timeout.
	/// </summary>
	Task WaitForJobAsync(string channel, TimeSpan timeout, CancellationToken ct = default);
}
