using AsyncEndpoints.Abstractions.Listener;
using AsyncEndpoints.Abstractions.Partitioning;
using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.Abstractions.Submission;
using AsyncEndpoints.Channels;
using AsyncEndpoints.Configuration;
using AsyncEndpoints.DependencyInjection;
using AsyncEndpoints.Execution;
using AsyncEndpoints.Infrastructure.Serialization;
using AsyncEndpoints.Partitioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.Core.UnitTests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAsyncEndpointsCore_RegistersAllServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IJobStore>());
        services.AddSingleton(Mock.Of<ILogger<JobDispatcher>>());
        services.AddAsyncEndpointsCore();

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IJobSubmitter>());
        Assert.NotNull(provider.GetService<IJobListener>());
        Assert.NotNull(provider.GetService<IHandlerRegistry>());
        Assert.NotNull(provider.GetService<JobDispatcher>());
        Assert.NotNull(provider.GetService<ChannelManager>());
        Assert.NotNull(provider.GetService<ISerializer>());
        Assert.NotNull(provider.GetService<IOptions<AsyncEndpointsOptions>>());
    }

    [Fact]
    public void AddAsyncEndpointsCore_ConfiguresOptions()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IJobStore>());
        services.AddSingleton(Mock.Of<ILogger<JobDispatcher>>());
        services.AddAsyncEndpointsCore(builder => builder.WithMaxConcurrency(16).WithMaxRetries(5));

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AsyncEndpointsOptions>>();

        Assert.Equal(16, options.Value.MaxConcurrency);
        Assert.Equal(5, options.Value.MaxRetries);
    }

    [Fact]
    public void AddAsyncEndpointsCore_ResolvesIJobSubmitter()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IJobStore>());
        services.AddSingleton(Mock.Of<ILogger<JobDispatcher>>());
        services.AddAsyncEndpointsCore();

        var provider = services.BuildServiceProvider();
        var submitter = provider.GetService<IJobSubmitter>();

        Assert.NotNull(submitter);
    }

    [Fact]
    public void AddAsyncEndpointsCore_WithPartitioning_RegistersPartitionServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IJobStore>());
        services.AddSingleton(Mock.Of<ILogger<JobDispatcher>>());
        services.AddAsyncEndpointsCore(builder => builder.EnablePartitioning(true));

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IPartitionAssigner>());
        Assert.NotNull(provider.GetService<PartitionManager>());
    }

    [Fact]
    public void AddAsyncEndpointsCore_WithoutPartitioning_DoesNotRegisterPartitionServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IJobStore>());
        services.AddSingleton(Mock.Of<ILogger<JobDispatcher>>());
        services.AddAsyncEndpointsCore();

        var provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<IPartitionAssigner>());
        Assert.Null(provider.GetService<PartitionManager>());
    }

    [Fact]
    public void AddAsyncEndpointsCore_CreatesDefaultChannelConfig()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IJobStore>());
        services.AddSingleton(Mock.Of<ILogger<JobDispatcher>>());
        services.AddAsyncEndpointsCore(builder => builder.WithDefaultChannel("custom-channel"));

        var provider = services.BuildServiceProvider();
        var channelManager = provider.GetRequiredService<ChannelManager>();

        var config = channelManager.GetChannelConfig("custom-channel");
        Assert.NotNull(config);
        Assert.Equal("custom-channel", config.Name);
    }
}
