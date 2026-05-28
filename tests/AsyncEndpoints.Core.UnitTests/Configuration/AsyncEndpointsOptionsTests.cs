using AsyncEndpoints.Core.Configuration;
using AsyncEndpoints.Worker.Hosting;

namespace AsyncEndpoints.Core.UnitTests.Configuration;

public class AsyncEndpointsOptionsTests
{
	[Fact]
	public void AsyncEndpointsOptions_HasCorrectDefaults()
	{
		var options = new AsyncEndpointsOptions();

		Assert.Equal(Environment.ProcessorCount, options.MaxConcurrency);
		Assert.Equal(3, options.MaxRetries);
		Assert.Equal(TimeSpan.FromSeconds(30), options.HeartbeatInterval);
		Assert.Equal(TimeSpan.FromSeconds(120), options.StaleJobTimeout);
		Assert.Equal(TimeSpan.FromMilliseconds(100), options.PollingMinInterval);
		Assert.Equal(TimeSpan.FromSeconds(30), options.PollingMaxInterval);
		Assert.Equal("default", options.DefaultChannel);
		Assert.False(options.EnablePartitioning);
		Assert.True(options.ObservabilityEnabled);
		Assert.Null(options.SerializerOptions);
	}

	[Fact]
	public void AsyncEndpointsOptionsBuilder_FluentApiWorks()
	{
		var options = new AsyncEndpointsOptionsBuilder()
			.WithMaxConcurrency(8)
			.WithMaxRetries(5)
			.WithHeartbeatInterval(TimeSpan.FromSeconds(10))
			.WithStaleJobTimeout(TimeSpan.FromSeconds(60))
			.WithPollingInterval(TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(15))
			.WithDefaultChannel("test-channel")
			.EnablePartitioning(true)
			.WithObservability(false)
			.Build();

		Assert.Equal(8, options.MaxConcurrency);
		Assert.Equal(5, options.MaxRetries);
		Assert.Equal(TimeSpan.FromSeconds(10), options.HeartbeatInterval);
		Assert.Equal(TimeSpan.FromSeconds(60), options.StaleJobTimeout);
		Assert.Equal(TimeSpan.FromMilliseconds(200), options.PollingMinInterval);
		Assert.Equal(TimeSpan.FromSeconds(15), options.PollingMaxInterval);
		Assert.Equal("test-channel", options.DefaultChannel);
		Assert.True(options.EnablePartitioning);
		Assert.False(options.ObservabilityEnabled);
	}

	[Fact]
	public void AsyncEndpointsOptionsBuilder_Build_ReturnsExpectedValues()
	{
		var builder = new AsyncEndpointsOptionsBuilder();

		var result = builder.Build();

		Assert.NotNull(result);
		Assert.Equal(Environment.ProcessorCount, result.MaxConcurrency);
	}

	[Fact]
	public void ChannelBuilder_CreatesChannelConfigsCorrectly()
	{
		var builder = new ChannelBuilder();

		builder
			.AddChannel("channel-1", c => { c.MaxConcurrency = 4; c.MaxRetries = 2; })
			.AddChannel("channel-2", c => { c.MaxConcurrency = 8; c.MaxRetries = 5; });

		Assert.Equal(2, builder.Channels.Count);
		Assert.Equal("channel-1", builder.Channels[0].Name);
		Assert.Equal(4, builder.Channels[0].MaxConcurrency);
		Assert.Equal(2, builder.Channels[0].MaxRetries);
		Assert.Equal("channel-2", builder.Channels[1].Name);
		Assert.Equal(8, builder.Channels[1].MaxConcurrency);
		Assert.Equal(5, builder.Channels[1].MaxRetries);
	}

	[Fact]
	public void ChannelBuilder_Defaults_AreCorrect()
	{
		var builder = new ChannelBuilder();

		builder.AddChannel("default-channel");

		Assert.Single(builder.Channels);
		Assert.Equal("default-channel", builder.Channels[0].Name);
		Assert.Equal(Environment.ProcessorCount, builder.Channels[0].MaxConcurrency);
		Assert.Equal(3, builder.Channels[0].MaxRetries);
	}

	[Fact]
	public void PartitionOptions_HasCorrectDefaults()
	{
		var options = new PartitionOptions();

		Assert.Equal(4, options.PartitionCount);
		Assert.Equal(TimeSpan.FromSeconds(60), options.LeaseTimeout);
		Assert.Equal(TimeSpan.FromSeconds(120), options.RebalanceInterval);
	}

	[Fact]
	public void WorkerOptions_HasCorrectDefaults()
	{
		var options = new WorkerOptions();

		Assert.NotEqual(Guid.Empty, options.WorkerId);
		Assert.Equal(Environment.ProcessorCount, options.MaxConcurrency);
		Assert.Equal(TimeSpan.FromMilliseconds(100), options.PollingIntervalMin);
		Assert.Equal(TimeSpan.FromSeconds(30), options.PollingIntervalMax);
		Assert.Equal(TimeSpan.FromSeconds(30), options.HeartbeatInterval);
		Assert.Equal(TimeSpan.FromSeconds(120), options.StaleJobTimeout);
		Assert.Equal(50, options.MaxQueueSize);
	}

	[Fact]
	public void WorkerOptions_UniqueWorkerIdPerInstance()
	{
		var options1 = new WorkerOptions();
		var options2 = new WorkerOptions();

		Assert.NotEqual(options1.WorkerId, options2.WorkerId);
	}
}
