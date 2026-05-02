using AsyncEndpoints.Core.Serialization;
using System.Text.Json.Serialization;

namespace AsyncEndpoints.Core.UnitTests.Serialization;

public record TestJob(string Name, int Value);

[JsonSerializable(typeof(TestJob))]
internal partial class TestJsonContext : JsonSerializerContext
{
}

public class JobSerializerRegistryTests
{
	private readonly JobSerializerRegistry _registry = new();

	[Fact]
	public void Register_And_Serialize_ShouldWork()
	{
		// Arrange
		_registry.Register(TestJsonContext.Default.TestJob);
		var job = new TestJob("Test", 123);

		// Act
		var json = _registry.Serialize(job);
		var deserialized = _registry.Deserialize(nameof(TestJob), json);

		// Assert
		Assert.Equal("{\"Name\":\"Test\",\"Value\":123}", json);
		var typedJob = Assert.IsType<TestJob>(deserialized);
		Assert.Equal(job.Name, typedJob.Name);
		Assert.Equal(job.Value, typedJob.Value);
	}

	[Fact]
	public void Serialize_UnknownType_ShouldThrow()
	{
		// Arrange
		var job = new TestJob("Test", 123);

		// Act & Assert
		Assert.Throws<UnknownJobTypeException>(() => _registry.Serialize(job));
	}

	[Fact]
	public void Deserialize_InvalidJson_ShouldThrow()
	{
		// Arrange
		_registry.Register(TestJsonContext.Default.TestJob);

		// Act & Assert
		Assert.Throws<JobDeserializationException>(() =>
			_registry.Deserialize(nameof(TestJob), "invalid-json"));
	}
}
