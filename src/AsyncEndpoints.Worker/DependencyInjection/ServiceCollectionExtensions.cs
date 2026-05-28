using AsyncEndpoints.Worker.Concurrency;
using AsyncEndpoints.Worker.Execution;
using AsyncEndpoints.Worker.Heartbeat;
using AsyncEndpoints.Worker.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AsyncEndpoints.Worker.DependencyInjection;

public static class ServiceCollectionExtensions
{
	private static readonly HashSet<string> _registeredChannels = [];

	/// <summary>
	/// Registers the main background worker for the default channel.
	/// Each call to <c>AddChannelWorker</c> adds a dedicated polling loop for that channel.
	/// </summary>
	public static IServiceCollection AddAsyncEndpointsWorker(
		this IServiceCollection services,
		Action<WorkerOptions>? configure = null)
	{
		services.AddOptions<WorkerOptions>();
		if (configure is not null)
		{
			services.Configure(configure);
		}

		services.AddSingleton<WorkerConcurrencyManager>();
		services.AddSingleton<HeartbeatService>();
		services.AddSingleton<RetryHandler>();
		services.AddSingleton<JobExecutionPipeline>();

		services.AddSingleton<IHostedService>(sp =>
		{
			var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WorkerOptions>>();
			var channel = options.Value.DefaultChannel;
			EnsureChannelNotDuplicate(channel);
			return new JobWorkerService(
				channel,
				sp.GetRequiredService<Abstractions.Listener.IJobListener>(),
				sp.GetRequiredService<JobExecutionPipeline>(),
				sp.GetRequiredService<WorkerConcurrencyManager>(),
				sp.GetRequiredService<HeartbeatService>(),
				options,
				sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<JobWorkerService>>());
		});

		services.AddHostedService<StaleJobSweeper>();

		return services;
	}

	/// <summary>
	/// Adds a dedicated background worker for a specific channel with its own polling loop.
	/// Multiple calls for different channel names are supported (competing-consumers pattern).
	/// Duplicate registrations for the same channel name will throw.
	/// </summary>
	public static IServiceCollection AddChannelWorker(
		this IServiceCollection services,
		string channelName)
	{
		services.AddSingleton<IHostedService>(sp =>
		{
			var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WorkerOptions>>();
			EnsureChannelNotDuplicate(channelName);
			return new JobWorkerService(
				channelName,
				sp.GetRequiredService<Abstractions.Listener.IJobListener>(),
				sp.GetRequiredService<JobExecutionPipeline>(),
				sp.GetRequiredService<WorkerConcurrencyManager>(),
				sp.GetRequiredService<HeartbeatService>(),
				options,
				sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<JobWorkerService>>());
		});

		return services;
	}

	private static void EnsureChannelNotDuplicate(string channel)
	{
		if (!_registeredChannels.Add(channel))
		{
			throw new InvalidOperationException(
				$"A worker for channel '{channel}' is already registered. " +
				"Use a different channel name, or if competing-consumers are intentional, " +
				"register the worker manually via AddSingleton<IHostedService>.");
		}
	}
}
