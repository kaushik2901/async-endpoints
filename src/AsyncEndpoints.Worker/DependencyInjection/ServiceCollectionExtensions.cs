using AsyncEndpoints.Worker.Concurrency;
using AsyncEndpoints.Worker.Execution;
using AsyncEndpoints.Worker.Heartbeat;
using AsyncEndpoints.Worker.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AsyncEndpoints.Worker.DependencyInjection;

public static class ServiceCollectionExtensions
{
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
			return new JobWorkerService(
				options.Value.DefaultChannel,
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

	public static IServiceCollection AddChannelWorker(
		this IServiceCollection services,
		string channelName)
	{
		services.AddSingleton<IHostedService>(sp =>
		{
			var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WorkerOptions>>();
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
}
