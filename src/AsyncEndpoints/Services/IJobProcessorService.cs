using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Entities;

namespace AsyncEndpoints.Services;

public interface IJobProcessorService
{
    Task ProcessAsync(Job job, CancellationToken cancellationToken);
}
