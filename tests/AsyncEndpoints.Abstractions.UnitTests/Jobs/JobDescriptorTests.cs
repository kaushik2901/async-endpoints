using AsyncEndpoints.Abstractions.Jobs;

namespace AsyncEndpoints.Abstractions.UnitTests.Jobs;

public class JobDescriptorTests
{
	[Fact]
	public void Constructor_SetsProperties()
	{
		var metadata = new Dictionary<string, string> { ["key"] = "value" };
		var descriptor = new JobDescriptor("test-job", "{ \"data\": 1 }", "critical", 10, "pk-1", metadata);

		Assert.Equal("test-job", descriptor.JobName);
		Assert.Equal("{ \"data\": 1 }", descriptor.Payload);
		Assert.Equal("critical", descriptor.Channel);
		Assert.Equal(10, descriptor.Priority);
		Assert.Equal("pk-1", descriptor.PartitionKey);
		Assert.Same(metadata, descriptor.Metadata);
	}

	[Fact]
	public void DefaultValues_AreCorrect()
	{
		var descriptor = new JobDescriptor("test-job", "{}");

		Assert.Equal("default", descriptor.Channel);
		Assert.Equal(0, descriptor.Priority);
		Assert.Null(descriptor.PartitionKey);
		Assert.Null(descriptor.Metadata);
	}

	[Theory]
	[InlineData("job-a", "{}", "default")]
	[InlineData("job-b", "{\"x\":1}", "events")]
	public void Constructor_WithDifferentValues_SetsCorrectly(string jobName, string payload, string channel)
	{
		var descriptor = new JobDescriptor(jobName, payload, channel);

		Assert.Equal(jobName, descriptor.JobName);
		Assert.Equal(payload, descriptor.Payload);
		Assert.Equal(channel, descriptor.Channel);
	}
}
