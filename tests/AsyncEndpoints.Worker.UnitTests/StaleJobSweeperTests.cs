using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.Worker.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AsyncEndpoints.Worker.UnitTests;

public class StaleJobSweeperTests
{
    [Fact]
    public async Task ExecuteAsync_CallsReclaimStaleJobs()
    {
        var mockStore = new Mock<IJobStore>();
        var options = new WorkerOptions { StaleJobTimeout = TimeSpan.FromMilliseconds(100) };
        var logger = Mock.Of<ILogger<StaleJobSweeper>>();

        var sweeper = new StaleJobSweeper(mockStore.Object, Options.Create(options), logger);

        using var cts = new CancellationTokenSource(350);

        await sweeper.StartAsync(cts.Token);
        await Task.Delay(400);
        await sweeper.StopAsync(CancellationToken.None);

        mockStore.Verify(s => s.ReclaimStaleJobsAsync(
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.AtLeast(1));
    }
}
