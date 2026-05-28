using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.Core.Infrastructure.Serialization;
using AsyncEndpoints.Core.Submission;
using Moq;

namespace AsyncEndpoints.Core.UnitTests;

public class JobSubmitterTests
{
	[Fact]
	public async Task SubmitAsync_SerializesJob_AndCallsStore()
	{
		var mockStore = new Mock<IJobStore>();
		var mockSerializer = new Mock<ISerializer>();
		var payload = new { Value = "test" };
		var serialized = "{\"Value\":\"test\"}";

		mockSerializer.Setup(s => s.Serialize(payload, (System.Text.Json.JsonSerializerOptions?)null)).Returns(serialized);
		mockStore.Setup(s => s.EnqueueAsync(It.IsAny<JobDescriptor>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Guid.NewGuid());

		var submitter = new JobSubmitter(mockStore.Object, mockSerializer.Object);
		var id = await submitter.SubmitAsync(payload, null, null, CancellationToken.None);

		Assert.NotEqual(Guid.Empty, id);
		mockSerializer.Verify(s => s.Serialize(payload, (System.Text.Json.JsonSerializerOptions?)null), Times.Once);
		mockStore.Verify(s => s.EnqueueAsync(It.Is<JobDescriptor>(d => d.Payload == serialized), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task SubmitAsync_ReturnsJobId_FromStore()
	{
		var mockStore = new Mock<IJobStore>();
		var mockSerializer = new Mock<ISerializer>();
		var expectedId = Guid.NewGuid();

		mockSerializer.Setup(s => s.Serialize(It.IsAny<object>(), (System.Text.Json.JsonSerializerOptions?)null)).Returns("{}");
		mockStore.Setup(s => s.EnqueueAsync(It.IsAny<JobDescriptor>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(expectedId);

		var submitter = new JobSubmitter(mockStore.Object, mockSerializer.Object);
		var id = await submitter.SubmitAsync(new { Value = "test" }, null, null, CancellationToken.None);

		Assert.Equal(expectedId, id);
	}

	[Fact]
	public async Task SubmitAsync_UsesDefaultChannel_WhenNoneSpecified()
	{
		var mockStore = new Mock<IJobStore>();
		var mockSerializer = new Mock<ISerializer>();

		mockSerializer.Setup(s => s.Serialize(It.IsAny<object>(), (System.Text.Json.JsonSerializerOptions?)null)).Returns("{}");
		mockStore.Setup(s => s.EnqueueAsync(It.IsAny<JobDescriptor>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Guid.NewGuid());

		var submitter = new JobSubmitter(mockStore.Object, mockSerializer.Object);
		await submitter.SubmitAsync(new { Value = "test" }, null, null, CancellationToken.None);

		mockStore.Verify(s => s.EnqueueAsync(It.Is<JobDescriptor>(d => d.Channel == "default"), It.IsAny<CancellationToken>()), Times.Once);
	}
}
