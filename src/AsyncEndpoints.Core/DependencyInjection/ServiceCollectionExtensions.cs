using AsyncEndpoints.Abstractions.Listener;
using AsyncEndpoints.Abstractions.Partitioning;
using AsyncEndpoints.Abstractions.Submission;
using AsyncEndpoints.Core.Channels;
using AsyncEndpoints.Core.Configuration;
using AsyncEndpoints.Core.Execution;
using AsyncEndpoints.Core.Infrastructure.Serialization;
using AsyncEndpoints.Core.Listener;
using AsyncEndpoints.Core.Partitioning;
using AsyncEndpoints.Core.Submission;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddAsyncEndpointsCore(this IServiceCollection services, Action<AsyncEndpointsOptionsBuilder>? configure = null)
	{
		var optionsBuilder = new AsyncEndpointsOptionsBuilder();
		configure?.Invoke(optionsBuilder);
		var options = optionsBuilder.Build();

		services.AddSingleton(options);
		services.AddSingleton<IOptions<AsyncEndpointsOptions>>(new OptionsWrapper<AsyncEndpointsOptions>(options));

		services.TryAddSingleton<ISerializer, Serializer>();

		services.TryAddSingleton<IJobSubmitter, JobSubmitter>();
		services.TryAddSingleton<IJobListener, PollingJobListener>();
		services.TryAddSingleton<IHandlerRegistry, HandlerRegistry>();
		services.TryAddSingleton<JobDispatcher>();

		var defaultChannelConfig = new ChannelConfig
		{
			Name = options.DefaultChannel,
			MaxConcurrency = options.MaxConcurrency,
			MaxRetries = options.MaxRetries
		};
		services.TryAddSingleton(new ChannelManager([defaultChannelConfig]));

		if (options.EnablePartitioning)
		{
			var partitionOptions = new PartitionOptions();
			services.TryAddSingleton<IPartitionAssigner>(sp =>
				new LeaseBasedPartitionAssigner(partitionOptions.PartitionCount));
			services.TryAddSingleton<PartitionManager>();
		}

		return services;
	}
}
