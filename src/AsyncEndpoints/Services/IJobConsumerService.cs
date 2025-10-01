using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AsyncEndpoints.Entities;

namespace AsyncEndpoints.Services
{
	/// <summary>
	/// Defines a contract for consuming jobs from a channel and processing them.
	/// </summary>
	public interface IJobConsumerService
	{
		/// <summary>
		/// Consumes jobs from the provided channel and processes them asynchronously.
		/// </summary>
		/// <param name="readerJobChannel">The channel reader to read jobs from.</param>
		/// <param name="semaphoreSlim">The semaphore to control concurrency.</param>
		/// <param name="stoppingToken">A cancellation token to stop the consumption process.</param>
		/// <returns>A task representing the asynchronous operation.</returns>
		Task ConsumeJobsAsync(ChannelReader<Job> readerJobChannel, SemaphoreSlim semaphoreSlim, CancellationToken stoppingToken);
	}
}