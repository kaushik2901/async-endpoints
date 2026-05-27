using AsyncEndpoints.Worker.Hosting;

namespace AsyncEndpoints.Background;

/// <summary>
/// Calculates appropriate delays based on the current state of job processing
/// </summary>
public interface IDelayCalculatorService
{
	/// <summary>
	/// Calculates the appropriate delay based on the current state and base polling interval
	/// </summary>
	/// <param name="state">The current state of job processing</param>
	/// <param name="workerOptions">The worker options</param>
	/// <returns>The calculated delay as a TimeSpan</returns>
	TimeSpan CalculateDelay(JobClaimingState state, WorkerOptions workerOptions);
}
