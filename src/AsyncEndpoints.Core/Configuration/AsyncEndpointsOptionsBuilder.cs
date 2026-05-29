using System.Text.Json;

namespace AsyncEndpoints.Core.Configuration;

public sealed class AsyncEndpointsOptionsBuilder
{
	private readonly AsyncEndpointsOptions _options = new();

	public AsyncEndpointsOptionsBuilder WithMaxConcurrency(int maxConcurrency)
	{
		_options.MaxConcurrency = maxConcurrency;
		return this;
	}

	public AsyncEndpointsOptionsBuilder WithMaxRetries(int maxRetries)
	{
		_options.MaxRetries = maxRetries;
		return this;
	}

	public AsyncEndpointsOptionsBuilder WithRetryDelayBaseSeconds(double seconds)
	{
		_options.RetryDelayBaseSeconds = seconds;
		return this;
	}

	public AsyncEndpointsOptionsBuilder WithHeartbeatInterval(TimeSpan interval)
	{
		_options.HeartbeatInterval = interval;
		return this;
	}

	public AsyncEndpointsOptionsBuilder WithStaleJobTimeout(TimeSpan timeout)
	{
		_options.StaleJobTimeout = timeout;
		return this;
	}

	public AsyncEndpointsOptionsBuilder WithPollingInterval(TimeSpan min, TimeSpan max)
	{
		_options.PollingMinInterval = min;
		_options.PollingMaxInterval = max;
		return this;
	}

	public AsyncEndpointsOptionsBuilder WithDefaultChannel(string channel)
	{
		_options.DefaultChannel = channel;
		return this;
	}

	public AsyncEndpointsOptionsBuilder EnablePartitioning(bool enabled)
	{
		_options.EnablePartitioning = enabled;
		return this;
	}

	public AsyncEndpointsOptionsBuilder WithObservability(bool enabled)
	{
		_options.ObservabilityEnabled = enabled;
		return this;
	}

	public AsyncEndpointsOptionsBuilder WithSerializerOptions(JsonSerializerOptions serializerOptions)
	{
		_options.SerializerOptions = serializerOptions;
		return this;
	}

	public AsyncEndpointsOptions Build()
	{
		return _options;
	}
}
