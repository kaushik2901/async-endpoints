namespace AsyncEndpoints.Core.Configuration;

public sealed class ChannelBuilder
{
	private readonly List<ChannelOptions> _channels = new();

	public IReadOnlyList<ChannelOptions> Channels => _channels.AsReadOnly();

	public ChannelBuilder AddChannel(string name, Action<ChannelOptions>? configure = null)
	{
		var options = new ChannelOptions { Name = name };
		configure?.Invoke(options);
		_channels.Add(options);
		return this;
	}
}

public sealed class ChannelOptions
{
	public string Name { get; set; } = string.Empty;
	public int MaxConcurrency { get; set; } = Environment.ProcessorCount;
	public int MaxRetries { get; set; } = 3;
}
