using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Core.Execution;
using AsyncEndpoints.Core.Internal;
using AsyncEndpoints.Core.Serialization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace AsyncEndpoints.Core.UnitTests.Execution;

public record SampleJob(string Message);

public class JobDispatcherTests
{
	[Fact]
	public async Task DispatchAsync_ShouldInvokeCorrectHandler()
	{
		// Arrange
		var services = new ServiceCollection();
		var handler = Substitute.For<IJobHandler<SampleJob>>();
		services.AddSingleton(handler);
		services.AddTransient<JobExecutor<SampleJob>>();

		var sp = services.BuildServiceProvider();

		var typeRegistry = new JobTypeRegistry();
		typeRegistry.Register<SampleJob>();

		var serializerRegistry = new JobSerializerRegistry();
		// Manual registration for test without source generator if needed, 
		// but let's use a real context for consistency.
		serializerRegistry.Register(SampleJobContext.Default.SampleJob);

		var dispatcher = new JobDispatcher(sp, typeRegistry, serializerRegistry);

		var record = new JobRecord
		{
			PayloadType = nameof(SampleJob),
			Payload = "{\"Message\":\"Hello\"}"
		};

		// Act
		await dispatcher.DispatchAsync(record, CancellationToken.None);

		// Assert
		await handler.Received(1).HandleAsync(Arg.Is<SampleJob>(j => j.Message == "Hello"), Arg.Any<CancellationToken>());
	}
}

[System.Text.Json.Serialization.JsonSerializable(typeof(SampleJob))]
internal partial class SampleJobContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
