using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.Core.Submission;
using Moq;

namespace AsyncEndpoints.Core.UnitTests;

public class JobSubmitterTests
{
	[Fact]
	public async Task SubmitAsync_CreatesJobDescriptor_AndCallsStore()
	{
		var mockStore = new Mock<IJobStore>();
		mockStore.Setup(s => s.EnqueueAsync(It.IsAny<JobDescriptor>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Guid.NewGuid());

		var submitter = new JobSubmitter(mockStore.Object);
		var id = await submitter.SubmitAsync("test-job", "{\"key\":\"value\"}", "my-channel", "pk-1", CancellationToken.None);

		Assert.NotEqual(Guid.Empty, id);
		mockStore.Verify(s => s.EnqueueAsync(
			It.Is<JobDescriptor>(d =>
				d.JobName == "test-job" &&
				d.Payload == "{\"key\":\"value\"}" &&
				d.Channel == "my-channel" &&
				d.PartitionKey == "pk-1"),
			It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task SubmitAsync_ReturnsJobId_FromStore()
	{
		var expectedId = Guid.NewGuid();
		var mockStore = new Mock<IJobStore>();
		mockStore.Setup(s => s.EnqueueAsync(It.IsAny<JobDescriptor>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(expectedId);

		var submitter = new JobSubmitter(mockStore.Object);
		var id = await submitter.SubmitAsync("test-job", "{}", null, null, CancellationToken.None);

		Assert.Equal(expectedId, id);
	}

	[Fact]
	public async Task SubmitAsync_UsesDefaultChannel_WhenNoneSpecified()
	{
		var mockStore = new Mock<IJobStore>();
		mockStore.Setup(s => s.EnqueueAsync(It.IsAny<JobDescriptor>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Guid.NewGuid());

		var submitter = new JobSubmitter(mockStore.Object);
		await submitter.SubmitAsync("test-job", "{}", null, null, CancellationToken.None);

		mockStore.Verify(s => s.EnqueueAsync(
			It.Is<JobDescriptor>(d => d.Channel == "default"),
			It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task SubmitAsync_UsesSpecifiedPartitionKey()
	{
		var mockStore = new Mock<IJobStore>();
		mockStore.Setup(s => s.EnqueueAsync(It.IsAny<JobDescriptor>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Guid.NewGuid());

		var submitter = new JobSubmitter(mockStore.Object);
		await submitter.SubmitAsync("test-job", "{}", null, "my-partition", CancellationToken.None);

		mockStore.Verify(s => s.EnqueueAsync(
			It.Is<JobDescriptor>(d => d.PartitionKey == "my-partition"),
			It.IsAny<CancellationToken>()), Times.Once);
	}
}
