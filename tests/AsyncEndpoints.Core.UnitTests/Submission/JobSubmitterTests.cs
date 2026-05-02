using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Listener;
using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.Core.Serialization;
using AsyncEndpoints.Core.Submission;
using NSubstitute;

namespace AsyncEndpoints.Core.UnitTests.Submission;

public record SubmissionJob(string Data);

public class JobSubmitterTests
{
	[Fact]
	public async Task SubmitAsync_ShouldEnqueueJobCorrectly()
	{
		// Arrange
		var store = Substitute.For<IJobStore>();
		var serializerRegistry = new JobSerializerRegistry();
		serializerRegistry.Register(SubmissionJobContext.Default.SubmissionJob);

		var submitter = new JobSubmitter(store, serializerRegistry);
		var job = new SubmissionJob("Some data");

		// Act
		var result = await submitter.SubmitAsync(job, channel: "reports", priority: 5, partitionBy: "user-1");

		// Assert
		Assert.Equal("reports", result.Channel);
		Assert.Equal(5, result.Priority);
		Assert.Equal("user-1", result.PartitionKey);
		Assert.Equal(nameof(SubmissionJob), result.PayloadType);

		await store.Received(1).EnqueueAsync(Arg.Is<JobDescriptor>(d =>
			d.Channel == "reports" &&
			d.Priority == 5 &&
			d.PartitionKey == "user-1" &&
			d.PayloadType == nameof(SubmissionJob)
		), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task SubmitAsync_WithNotifier_ShouldInvokeNotifier()
	{
		// Arrange
		var store = Substitute.For<IJobStore>();
		var notifier = Substitute.For<IJobNotifier>();
		var serializerRegistry = new JobSerializerRegistry();
		serializerRegistry.Register(SubmissionJobContext.Default.SubmissionJob);

		var submitter = new JobSubmitter(store, serializerRegistry, notifier);
		var job = new SubmissionJob("Some data");

		// Act
		await submitter.SubmitAsync(job, channel: "reports");

		// Assert
		await store.Received(1).EnqueueAsync(Arg.Any<JobDescriptor>(), Arg.Any<CancellationToken>());
		await notifier.Received(1).NotifyJobAvailableAsync("reports", Arg.Any<CancellationToken>());
	}
}

[System.Text.Json.Serialization.JsonSerializable(typeof(SubmissionJob))]
internal partial class SubmissionJobContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
