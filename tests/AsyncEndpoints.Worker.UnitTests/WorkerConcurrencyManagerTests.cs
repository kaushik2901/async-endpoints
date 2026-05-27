using AsyncEndpoints.Worker.Concurrency;
using AsyncEndpoints.Worker.Hosting;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.Worker.UnitTests;

public class WorkerConcurrencyManagerTests
{
    [Fact]
    public async Task CurrentCount_ReturnsMaxConcurrency_Initially()
    {
        var options = new WorkerOptions { MaxConcurrency = 5 };
        var manager = new WorkerConcurrencyManager(Options.Create(options));

        Assert.Equal(5, manager.CurrentCount("default"));
    }

    [Fact]
    public async Task WaitAsync_Blocks_WhenAtMaxConcurrency()
    {
        var options = new WorkerOptions { MaxConcurrency = 1 };
        var manager = new WorkerConcurrencyManager(Options.Create(options));

        await manager.WaitAsync("default", null, default);

        var blocked = Task.Run(async () =>
        {
            using var cts = new CancellationTokenSource(100);
            try
            {
                await manager.WaitAsync("default", null, cts.Token);
                return false;
            }
            catch (OperationCanceledException)
            {
                return true;
            }
        });

        var wasBlocked = await blocked;
        Assert.True(wasBlocked);
    }

    [Fact]
    public async Task Release_AllowsNextJob()
    {
        var options = new WorkerOptions { MaxConcurrency = 1 };
        var manager = new WorkerConcurrencyManager(Options.Create(options));

        await manager.WaitAsync("default", null, default);
        manager.Release("default", null);

        await manager.WaitAsync("default", null, default);
        manager.Release("default", null);

        Assert.Equal(1, manager.CurrentCount("default"));
    }

    [Fact]
    public async Task CurrentCount_DecreasesAfterWait()
    {
        var options = new WorkerOptions { MaxConcurrency = 5 };
        var manager = new WorkerConcurrencyManager(Options.Create(options));

        await manager.WaitAsync("default", null, default);

        Assert.Equal(4, manager.CurrentCount("default"));
    }

    [Fact]
    public async Task PerChannelSemaphores_AreIsolated()
    {
        var options = new WorkerOptions { MaxConcurrency = 1 };
        var manager = new WorkerConcurrencyManager(Options.Create(options));

        await manager.WaitAsync("channel-a", null, default);

        var channelBAvailable = await Task.Run(async () =>
        {
            using var cts = new CancellationTokenSource(100);
            try
            {
                await manager.WaitAsync("channel-b", null, cts.Token);
                manager.Release("channel-b", null);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        });

        Assert.True(channelBAvailable);
        Assert.Equal(0, manager.CurrentCount("channel-a"));
        Assert.Equal(1, manager.CurrentCount("channel-b"));
    }

    [Fact]
    public async Task PartitionSemaphore_EnsuresOrdering()
    {
        var options = new WorkerOptions { MaxConcurrency = 5 };
        var manager = new WorkerConcurrencyManager(Options.Create(options));

        await manager.WaitAsync("default", 1, default);

        var samePartitionBlocked = Task.Run(async () =>
        {
            using var cts = new CancellationTokenSource(100);
            try
            {
                await manager.WaitAsync("default", 1, cts.Token);
                return false;
            }
            catch (OperationCanceledException)
            {
                return true;
            }
        });

        var wasBlocked = await samePartitionBlocked;
        Assert.True(wasBlocked);

        manager.Release("default", 1);
    }

    [Fact]
    public async Task Dispose_ReleasesAllSemaphores()
    {
        var options = new WorkerOptions { MaxConcurrency = 2 };
        var manager = new WorkerConcurrencyManager(Options.Create(options));

        await manager.WaitAsync("default", null, default);
        manager.Dispose();

        Assert.Equal(2, manager.CurrentCount("default"));
    }
}
