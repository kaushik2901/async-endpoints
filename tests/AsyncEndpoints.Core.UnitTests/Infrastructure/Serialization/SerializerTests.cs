using AsyncEndpoints.Core.Infrastructure.Serialization;
using System.Text.Json;

namespace AsyncEndpoints.Core.UnitTests.Infrastructure.Serialization;

public class SerializerTests
{
	[Fact]
	public void Constructor_Succeeds_WithValidJsonOptions()
	{
		var serializer = new Serializer();

		Assert.NotNull(serializer);
	}

	[Fact]
	public void Serialize_Generic_WithDefaultOptions_SerializesCorrectly()
	{
		var serializer = new Serializer();
		var testObject = new { Name = "Test", Value = 123 };

		var result = serializer.Serialize(testObject, (System.Text.Json.JsonSerializerOptions?)null);

		Assert.NotNull(result);
		Assert.Contains("Test", result);
		Assert.Contains("123", result);
	}

	[Fact]
	public void Serialize_Generic_WithCustomOptions_SerializesWithCustomOptions()
	{
		var serializer = new Serializer();
		var customOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
		var testObject = new TestClass { PropertyName = "TestValue" };

		var result = serializer.Serialize(testObject, customOptions);

		Assert.NotNull(result);
		Assert.Contains("propertyName", result);
		Assert.DoesNotContain("PropertyName", result);
	}

	[Fact]
	public void Serialize_NonGeneric_SerializesCorrectly()
	{
		var serializer = new Serializer();
		var testObject = new { Name = "Test", Value = 123 };
		var type = typeof(object);

		var result = serializer.Serialize(testObject, type, (System.Text.Json.JsonSerializerOptions?)null);

		Assert.NotNull(result);
		Assert.Contains("Test", result);
		Assert.Contains("123", result);
	}

	[Fact]
	public void Deserialize_Generic_Succeeds_WithValidJson()
	{
		var serializer = new Serializer();
		var json = "{\"name\":\"Test\",\"value\":123}";

		var result = serializer.Deserialize<TestDto>((string)json, (System.Text.Json.JsonSerializerOptions?)null);

		Assert.NotNull(result);
		Assert.Equal("Test", result.Name);
		Assert.Equal(123, result.Value);
	}

	[Fact]
	public void Deserialize_Generic_WithCustomOptions_DeserializesWithCustomOptions()
	{
		var serializer = new Serializer();
		var customOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		var json = "{\"propertyName\":\"TestValue\"}";

		var result = serializer.Deserialize<TestClass>((string)json, customOptions);

		Assert.NotNull(result);
		Assert.Equal("TestValue", result.PropertyName);
	}

	[Fact]
	public void Deserialize_NonGeneric_Succeeds_WithValidJson()
	{
		var serializer = new Serializer();
		var json = "{\"name\":\"Test\",\"value\":123}";
		var type = typeof(TestDto);

		var result = serializer.Deserialize(json, type, (System.Text.Json.JsonSerializerOptions?)null);

		Assert.NotNull(result);
		var typedResult = result as TestDto;
		Assert.NotNull(typedResult);
		Assert.Equal("Test", typedResult.Name);
		Assert.Equal(123, typedResult.Value);
	}

	[Fact]
	public void Deserialize_ThrowsException_WhenNullJsonProvided()
	{
		var serializer = new Serializer();

		var exception = Record.Exception(() => serializer.Deserialize<TestDto>((string)null!, (System.Text.Json.JsonSerializerOptions?)null));

		Assert.NotNull(exception);
		Assert.IsType<ArgumentNullException>(exception);
		Assert.Contains("json", exception.Message);
	}

	[Fact]
	public void Deserialize_HandlesInvalidJson_Gracefully()
	{
		var serializer = new Serializer();
		var invalidJson = "{invalid json}";

		var exception = Record.Exception(() => serializer.Deserialize<TestDto>(invalidJson, (System.Text.Json.JsonSerializerOptions?)null));

		Assert.NotNull(exception);
		Assert.IsType<JsonException>(exception);
	}

	[Fact]
	public void Deserialize_Stream_Succeeds_WithValidJson()
	{
		var serializer = new Serializer();
		var json = "{\"name\":\"Test\",\"value\":123}";
		var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

		var result = serializer.Deserialize<TestDto>(stream, (System.Text.Json.JsonSerializerOptions?)null);

		Assert.NotNull(result);
		Assert.Equal("Test", result.Name);
		Assert.Equal(123, result.Value);
	}

	[Fact]
	public void Deserialize_Stream_HandlesInvalidJson_Gracefully()
	{
		var serializer = new Serializer();
		var invalidJson = "{invalid json}";
		var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(invalidJson));

		var exception = Record.Exception(() => serializer.Deserialize<TestDto>(stream, (System.Text.Json.JsonSerializerOptions?)null));

		Assert.NotNull(exception);
		Assert.IsType<JsonException>(exception);
	}

	[Fact]
	public void Deserialize_Stream_HandlesIOException_Gracefully()
	{
		var serializer = new Serializer();
		var stream = new MockStreamThatThrowsIOException();
		var validJson = "{\"name\":\"Test\",\"value\":123}";
		var jsonBytes = System.Text.Encoding.UTF8.GetBytes(validJson);

		stream.Write(jsonBytes, 0, jsonBytes.Length);
		stream.Seek(0, SeekOrigin.Begin);

		var exception = Record.Exception(() => serializer.Deserialize<TestDto>(stream, (System.Text.Json.JsonSerializerOptions?)null));

		Assert.NotNull(exception);
		Assert.IsType<InvalidOperationException>(exception);
		Assert.Contains("Error reading from stream", exception.Message);
	}

	[Fact]
	public async Task DeserializeAsync_Stream_Succeeds_WithValidJson()
	{
		var serializer = new Serializer();
		var json = "{\"name\":\"Test\",\"value\":123}";
		var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

		var result = await serializer.DeserializeAsync<TestDto>(stream, (System.Text.Json.JsonSerializerOptions?)null);

		Assert.NotNull(result);
		Assert.Equal("Test", result.Name);
		Assert.Equal(123, result.Value);
	}

	[Fact]
	public async Task DeserializeAsync_Stream_HandlesInvalidJson_Gracefully()
	{
		var serializer = new Serializer();
		var invalidJson = "{invalid json}";
		var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(invalidJson));

		var exception = await Record.ExceptionAsync(() => serializer.DeserializeAsync<TestDto>(stream, (System.Text.Json.JsonSerializerOptions?)null));

		Assert.NotNull(exception);
		Assert.IsType<JsonException>(exception);
	}

	private class TestClass
	{
		public string? PropertyName { get; set; }
	}

	private class TestDto
	{
		public string? Name { get; set; }
		public int Value { get; set; }
	}

	private class MockStreamThatThrowsIOException : Stream
	{
		private readonly MemoryStream _innerStream = new();
		private bool _throwOnRead = true;

		public override bool CanRead => true;
		public override bool CanSeek => true;
		public override bool CanWrite => true;
		public override long Length => _innerStream.Length;
		public override long Position { get => _innerStream.Position; set => _innerStream.Position = value; }

		public override void Flush() => _innerStream.Flush();
		public override int Read(byte[] buffer, int offset, int count)
		{
			if (_throwOnRead)
				throw new IOException("Simulated IO exception");
			return _innerStream.Read(buffer, offset, count);
		}

		public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
		public override void SetLength(long value) => _innerStream.SetLength(value);
		public override void Write(byte[] buffer, int offset, int count) => _innerStream.Write(buffer, offset, count);
	}
}
