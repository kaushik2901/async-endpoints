using AsyncEndpoints.Abstractions.Jobs;
using System.Text.Json;

namespace AsyncEndpoints.Abstractions.UnitTests.Jobs;

public class JobDescriptorTests
{
	[Fact]
	public void JobDescriptor_Serialization_ShouldWork()
	{
		// Arrange
		var descriptor = new JobDescriptor
		{
			JobId = Guid.NewGuid(),
			Channel = "test-channel",
			Priority = 10,
			PayloadType = "TestJob",
			Payload = "{\"foo\":\"bar\"}",
			MaxRetries = 5,
			RunAfter = DateTimeOffset.UtcNow.AddMinutes(5),
			PartitionKey = "entity-123"
		};

		// Act
		var json = JsonSerializer.Serialize(descriptor);
		var deserialized = JsonSerializer.Deserialize<JobDescriptor>(json);

		// Assert
		Assert.NotNull(deserialized);
		Assert.Equal(descriptor.JobId, deserialized.JobId);
		Assert.Equal(descriptor.Channel, deserialized.Channel);
		Assert.Equal(descriptor.Priority, deserialized.Priority);
		Assert.Equal(descriptor.PayloadType, deserialized.PayloadType);
		Assert.Equal(descriptor.Payload, deserialized.Payload);
		Assert.Equal(descriptor.MaxRetries, deserialized.MaxRetries);
		Assert.Equal(descriptor.RunAfter, deserialized.RunAfter);
		Assert.Equal(descriptor.PartitionKey, deserialized.PartitionKey);
	}
}
