using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AsyncEndpoints.JobProcessing;

namespace AsyncEndpoints.Background
{
	/// <summary>
	/// Defines a contract for producing jobs and writing them to a channel.
	/// </summary>
	public interface IJobProducerService
	{
		/// <summary>
		/// Produces jobs and writes them to the provided channel asynchronously.
		/// </summary>
		/// <param name="writerJobChannel">The channel writer to write jobs to.</param>
		/// <param name="stoppingToken">A cancellation token to stop the production process.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		Task ProduceJobsAsync(ChannelWriter<Job> writerJobChannel, CancellationToken stoppingToken);
	}
}
