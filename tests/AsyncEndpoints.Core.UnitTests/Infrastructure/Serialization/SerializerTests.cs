using AsyncEndpoints.Core.Infrastructure.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AsyncEndpoints.Core.UnitTests.Infrastructure.Serialization;

[JsonSerializable(typeof(TestDto))]
internal partial class TestJsonContext : JsonSerializerContext
{
}

public class SerializerTests
{
	private static readonly JsonSerializerContext TestContext = TestJsonContext.Default;
	private static readonly JsonSerializerContext[] Contexts = [TestContext];

	[Fact]
	public void Serialize_SerializesCorrectly()
	{
		var serializer = new Serializer(Contexts);
		var testObject = new TestDto { Name = "Test", Value = 123 };

		var result = serializer.Serialize(testObject);

		Assert.NotNull(result);
		Assert.Contains("Test", result);
		Assert.Contains("123", result);
	}

	[Fact]
	public void Serialize_Throws_ForUnregisteredType()
	{
		var serializer = new Serializer([]);
		var testObject = new { Name = "Test" };

		Assert.Throws<KeyNotFoundException>(() => serializer.Serialize(testObject));
	}

	[Fact]
	public void Deserialize_Succeeds_WithValidJson()
	{
		var serializer = new Serializer(Contexts);
		var json = "{\"Name\":\"Test\",\"Value\":123}";

		var result = serializer.Deserialize<TestDto>(json);

		Assert.NotNull(result);
		Assert.Equal("Test", result.Name);
		Assert.Equal(123, result.Value);
	}

	[Fact]
	public void Deserialize_Throws_ForUnregisteredType()
	{
		var serializer = new Serializer([]);

		Assert.Throws<KeyNotFoundException>(() => serializer.Deserialize<TestDto>("{}"));
	}

	[Fact]
	public void Deserialize_Throws_WhenJsonIsNull()
	{
		var serializer = new Serializer(Contexts);

		Assert.Throws<ArgumentNullException>(() => serializer.Deserialize<TestDto>(null!));
	}

	[Fact]
	public void Deserialize_Throws_ForInvalidJson()
	{
		var serializer = new Serializer(Contexts);

		Assert.Throws<JsonException>(() => serializer.Deserialize<TestDto>("{invalid json}"));
	}
}

public class TestDto
{
	public string? Name { get; set; }
	public int Value { get; set; }
}
