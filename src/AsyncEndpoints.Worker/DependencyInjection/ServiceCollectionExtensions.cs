using AsyncEndpoints.Core.Configuration;
using AsyncEndpoints.Worker.Concurrency;
using AsyncEndpoints.Worker.Execution;
using AsyncEndpoints.Worker.Heartbeat;
using AsyncEndpoints.Worker.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AsyncEndpoints.Worker.DependencyInjection;

public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the main background worker for the default channel.
	/// Configuration is read from <see cref="AsyncEndpointsOptions"/> (registered by <c>AddAsyncEndpointsCore</c>).
	/// </summary>
	public static IServiceCollection AddAsyncEndpointsWorker(
		this IServiceCollection services,
		Action<AsyncEndpointsOptions>? configure = null)
	{
		if (configure is not null)
		{
			services.PostConfigure<AsyncEndpointsOptions>(o => configure(o));
		}

		services.AddSingleton<WorkerConcurrencyManager>();
		services.AddSingleton<HeartbeatService>();
		services.AddSingleton<RetryHandler>();
		services.AddSingleton<JobExecutionPipeline>();

		services.AddSingleton<IHostedService>(sp =>
		{
			var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AsyncEndpointsOptions>>();
			var channel = options.Value.DefaultChannel;
			return new JobWorkerService(
				channel,
				sp.GetRequiredService<Abstractions.Listener.IJobListener>(),
				sp.GetRequiredService<JobExecutionPipeline>(),
				sp.GetRequiredService<WorkerConcurrencyManager>(),
				sp.GetRequiredService<HeartbeatService>(),
				sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<JobWorkerService>>());
		});

		services.AddHostedService<StaleJobSweeper>();

		return services;
	}

	/// <summary>
	/// Adds a dedicated background worker for a specific channel with its own polling loop.
	/// Multiple calls for the same channel are allowed (competing-consumers pattern).
	/// </summary>
	public static IServiceCollection AddChannelWorker(
		this IServiceCollection services,
		string channelName)
	{
		services.AddSingleton<IHostedService>(sp =>
		{
			return new JobWorkerService(
				channelName,
				sp.GetRequiredService<Abstractions.Listener.IJobListener>(),
				sp.GetRequiredService<JobExecutionPipeline>(),
				sp.GetRequiredService<WorkerConcurrencyManager>(),
				sp.GetRequiredService<HeartbeatService>(),
				sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<JobWorkerService>>());
		});

		return services;
	}
}
