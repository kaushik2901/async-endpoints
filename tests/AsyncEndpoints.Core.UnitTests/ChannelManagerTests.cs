using AsyncEndpoints.Core.Channels;

namespace AsyncEndpoints.Core.UnitTests;

public class ChannelManagerTests
{
	[Fact]
	public void GetChannelNames_ReturnsConfiguredChannels()
	{
		var channels = new[]
		{
			new ChannelConfig { Name = "default", MaxConcurrency = 4, MaxRetries = 3 },
			new ChannelConfig { Name = "high-priority", MaxConcurrency = 2, MaxRetries = 5 }
		};
		var manager = new ChannelManager(channels);

		var names = manager.GetChannelNames();

		Assert.Contains("default", names);
		Assert.Contains("high-priority", names);
		Assert.Equal(2, names.Count);
	}

	[Fact]
	public void GetChannelConfig_ReturnsConfig_ForKnownChannel()
	{
		var channels = new[]
		{
			new ChannelConfig { Name = "default", MaxConcurrency = 8, MaxRetries = 3 }
		};
		var manager = new ChannelManager(channels);

		var config = manager.GetChannelConfig("default");

		Assert.NotNull(config);
		Assert.Equal("default", config.Name);
		Assert.Equal(8, config.MaxConcurrency);
		Assert.Equal(3, config.MaxRetries);
	}

	[Fact]
	public void GetChannelConfig_ReturnsNull_ForUnknownChannel()
	{
		var channels = new[]
		{
			new ChannelConfig { Name = "default", MaxConcurrency = 4, MaxRetries = 3 }
		};
		var manager = new ChannelManager(channels);

		var config = manager.GetChannelConfig("nonexistent");

		Assert.Null(config);
	}
}
