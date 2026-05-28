using AsyncEndpoints.Core.Channels;

namespace AsyncEndpoints.Core.Configuration;

public sealed class ChannelBuilder
{
	private readonly List<ChannelConfig> _channels = new();

	public IReadOnlyList<ChannelConfig> Channels => _channels.AsReadOnly();

	public ChannelBuilder AddChannel(string name, Action<ChannelConfig>? configure = null)
	{
		var config = new ChannelConfig { Name = name };
		configure?.Invoke(config);
		_channels.Add(config);
		return this;
	}
}
