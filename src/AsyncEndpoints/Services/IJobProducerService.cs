using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AsyncEndpoints.Entities;

namespace AsyncEndpoints.Services
{
    public interface IJobProducerService
    {
        Task ProduceJobsAsync(ChannelWriter<Job> _writerJobChannel, CancellationToken stoppingToken);
    }
}