using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace AsyncEndpoints.BackgroundWorker;

public class AsyncEndpointsBackgroundService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}
