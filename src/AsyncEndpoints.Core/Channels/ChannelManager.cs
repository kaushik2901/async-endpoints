namespace AsyncEndpoints.Core.Channels;

public sealed class ChannelManager
{
	private readonly IReadOnlyDictionary<string, ChannelConfig> _channels;

	public ChannelManager(IEnumerable<ChannelConfig> channels)
	{
		_channels = channels.ToDictionary(c => c.Name, c => c);
	}

	public IReadOnlyList<string> GetChannelNames() => _channels.Keys.ToList().AsReadOnly();

	public ChannelConfig? GetChannelConfig(string name) =>
		_channels.TryGetValue(name, out var config) ? config : null;

	public IReadOnlyDictionary<string, ChannelConfig> GetConfiguredChannels() => _channels;
}
