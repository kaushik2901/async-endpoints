using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AsyncEndpoints.JobProcessing;

namespace AsyncEndpoints.Background;

/// <summary>
/// Defines a contract for enqueuing jobs to a channel writer
/// </summary>
public interface IJobChannelEnqueuer
{
	/// <summary>
	/// Attempts to enqueue a job to the provided channel writer
	/// </summary>
	/// <param name="writerJobChannel">The channel writer to write the job to</param>
	/// <param name="job">The job to enqueue</param>
	/// <param name="stoppingToken">Cancellation token</param>
	/// <returns>True if successfully enqueued, false otherwise</returns>
	Task<bool> Enqueue(ChannelWriter<Job> writerJobChannel, Job job, CancellationToken stoppingToken);
}
