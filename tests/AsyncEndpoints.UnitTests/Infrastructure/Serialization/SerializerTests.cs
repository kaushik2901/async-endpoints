using System.Text.Json;
using AsyncEndpoints.Infrastructure.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.UnitTests.Infrastructure.Serialization;

public class SerializerTests
{
	/// <summary>
	/// Verifies that the Serializer can be constructed with valid dependencies without throwing an exception.
	/// This test ensures the constructor properly accepts and stores the required JsonOptions.
	/// </summary>
	[Fact]
	public void Constructor_Succeeds_WithValidJsonOptions()
	{
		// Arrange
		var jsonOptions = Options.Create(new JsonOptions());

		// Act
		var serializer = new Serializer(jsonOptions);

		// Assert
		Assert.NotNull(serializer);
	}

	/// <summary>
	/// Verifies that the generic Serialize method correctly serializes an object to JSON string.
	/// This test ensures proper serialization functionality with default options.
	/// </summary>
	[Fact]
	public void Serialize_Generic_WithDefaultOptions_SerializesCorrectly()
	{
		// Arrange
		var jsonOptions = Options.Create(new JsonOptions());
		var serializer = new Serializer(jsonOptions);
		var testObject = new { Name = "Test", Value = 123 };

		// Act
		var result = serializer.Serialize(testObject);

		// Assert
		Assert.NotNull(result);
		Assert.Contains("Test", result);
		Assert.Contains("123", result);
	}

	/// <summary>
	/// Verifies that the generic Serialize method correctly serializes an object to JSON string with custom options.
	/// This test ensures the method respects custom JsonSerializerOptions when provided.
	/// </summary>
	[Fact]
	public void Serialize_Generic_WithCustomOptions_SerializesWithCustomOptions()
	{
		// Arrange
		var jsonOptions = Options.Create(new JsonOptions());
		var serializer = new Serializer(jsonOptions);
		var customOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
		var testObject = new TestClass { PropertyName = "TestValue" };

		// Act
		var result = serializer.Serialize(testObject, customOptions);

		// Assert
		Assert.NotNull(result);
		Assert.Contains("propertyName", result); // camelCase due to custom options
		Assert.DoesNotContain("PropertyName", result); // PascalCase should not be present
	}

	/// <summary>
	/// Verifies that the non-generic Serialize method correctly serializes an object to JSON string.
	/// This test ensures proper serialization functionality when type information is provided explicitly.
	/// </summary>
	[Fact]
	public void Serialize_NonGeneric_SerializesCorrectly()
	{
		// Arrange
		var jsonOptions = Options.Create(new JsonOptions());
		var serializer = new Serializer(jsonOptions);
		var testObject = new { Name = "Test", Value = 123 };
		var type = typeof(object);

		// Act
		var result = serializer.Serialize(testObject, type);

		// Assert
		Assert.NotNull(result);
		Assert.Contains("Test", result);
		Assert.Contains("123", result);
	}

	/// <summary>
	/// Verifies that the generic Deserialize method correctly deserializes a JSON string to an object.
	/// This test ensures proper deserialization functionality with default options.
	/// </summary>
	[Fact]
	public void Deserialize_Generic_Succeeds_WithValidJson()
	{
		// Arrange
		var jsonOptions = Options.Create(new JsonOptions());
		var serializer = new Serializer(jsonOptions);
		var json = "{\"name\":\"Test\",\"value\":123}";

		// Act
		var result = serializer.Deserialize<TestDto>((string)json);

		// Assert
		Assert.NotNull(result);
		Assert.Equal("Test", result.Name);
		Assert.Equal(123, result.Value);
	}

	/// <summary>
	/// Verifies that the generic Deserialize method correctly deserializes a JSON string to an object with custom options.
	/// This test ensures the method respects custom JsonSerializerOptions when provided.
	/// </summary>
	[Fact]
	public void Deserialize_Generic_WithCustomOptions_DeserializesWithCustomOptions()
	{
		// Arrange
		var jsonOptions = Options.Create(new JsonOptions());
		var serializer = new Serializer(jsonOptions);
		var customOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		var json = "{\"propertyName\":\"TestValue\"}"; // camelCase JSON

		// Act
		var result = serializer.Deserialize<TestClass>((string)json, customOptions);

		// Assert
		Assert.NotNull(result);
		Assert.Equal("TestValue", result.PropertyName);
	}

	/// <summary>
	/// Verifies that the non-generic Deserialize method correctly deserializes a JSON string to an object.
	/// This test ensures proper deserialization functionality when type information is provided explicitly.
	/// </summary>
	[Fact]
	public void Deserialize_NonGeneric_Succeeds_WithValidJson()
	{
		// Arrange
		var jsonOptions = Options.Create(new JsonOptions());
		var serializer = new Serializer(jsonOptions);
		var json = "{\"name\":\"Test\",\"value\":123}";
		var type = typeof(TestDto);

		// Act
		var result = serializer.Deserialize(json, type);

		// Assert
		Assert.NotNull(result);
		var typedResult = result as TestDto;
		Assert.NotNull(typedResult);
		Assert.Equal("Test", typedResult.Name);
		Assert.Equal(123, typedResult.Value);
	}

	/// <summary>
	/// Verifies that deserialization throws appropriate exception when null JSON string is provided.
	/// This test ensures the serializer properly propagates exceptions from the underlying JSON library.
	/// </summary>
	[Fact]
	public void Deserialize_ThrowsException_WhenNullJsonProvided()
	{
		// Arrange
		var jsonOptions = Options.Create(new JsonOptions());
		var serializer = new Serializer(jsonOptions);

		// Act & Assert
		var exception = Record.Exception(() => serializer.Deserialize<TestDto>((string)null!));

		Assert.NotNull(exception);
		Assert.IsType<ArgumentNullException>(exception);
		Assert.Contains("json", exception.Message);
	}

	/// <summary>
	/// Verifies that deserialization handles invalid JSON gracefully.
	/// This test ensures the serializer handles malformed JSON appropriately.
	/// </summary>
	[Fact]
	public void Deserialize_HandlesInvalidJson_Gracefully()
	{
		// Arrange
		var jsonOptions = Options.Create(new JsonOptions());
		var serializer = new Serializer(jsonOptions);
		var invalidJson = "{invalid json}";

		// Act & Assert
		var exception = Record.Exception(() => serializer.Deserialize<TestDto>(invalidJson));

		Assert.NotNull(exception);
		Assert.IsType<JsonException>(exception);
	}

	/// <summary>
	/// Verifies that the stream-based Deserialize method correctly deserializes valid JSON from a stream.
	/// This test ensures proper deserialization functionality from stream input.
	/// </summary>
	[Fact]
	public void Deserialize_Stream_Succeeds_WithValidJson()
	{
		// Arrange
		var jsonOptions = Options.Create(new JsonOptions());
		var serializer = new Serializer(jsonOptions);
		var json = "{\"name\":\"Test\",\"value\":123}";
		var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

		// Act
		var result = serializer.Deserialize<TestDto>(stream);

		// Assert
		Assert.NotNull(result);
		Assert.Equal("Test", result.Name);
		Assert.Equal(123, result.Value);
	}

	/// <summary>
	/// Verifies that the stream-based Deserialize method handles invalid JSON appropriately.
	/// This test ensures proper exception handling when malformed JSON is provided via stream.
	/// </summary>
	[Fact]
	public void Deserialize_Stream_HandlesInvalidJson_Gracefully()
	{
		// Arrange
		var jsonOptions = Options.Create(new JsonOptions());
		var serializer = new Serializer(jsonOptions);
		var invalidJson = "{invalid json}";
		var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(invalidJson));

		// Act & Assert
		var exception = Record.Exception(() => serializer.Deserialize<TestDto>(stream));

		Assert.NotNull(exception);
		Assert.IsType<JsonException>(exception);
	}

	/// <summary>
	/// Verifies that the stream-based Deserialize method handles IOException appropriately.
	/// This test ensures proper error handling when stream reading issues occur.
	/// </summary>
	[Fact]
	public void Deserialize_Stream_HandlesIOException_Gracefully()
	{
		// Arrange
		var jsonOptions = Options.Create(new JsonOptions());
		var serializer = new Serializer(jsonOptions);
		var stream = new MockStreamThatThrowsIOException();
		var validJson = "{\"name\":\"Test\",\"value\":123}";
		var jsonBytes = System.Text.Encoding.UTF8.GetBytes(validJson);

		// We'll write the bytes to the stream but the mock will throw IOException on read
		stream.Write(jsonBytes, 0, jsonBytes.Length);
		stream.Seek(0, SeekOrigin.Begin);

		// Act & Assert
		var exception = Record.Exception(() => serializer.Deserialize<TestDto>(stream));

		Assert.NotNull(exception);
		Assert.IsType<InvalidOperationException>(exception);
		Assert.Contains("Error reading from stream", exception.Message);
	}

	/// <summary>
	/// Verifies that the async stream-based DeserializeAsync method correctly deserializes valid JSON from a stream.
	/// This test ensures proper async deserialization functionality from stream input.
	/// </summary>
	[Fact]
	public async Task DeserializeAsync_Stream_Succeeds_WithValidJson()
	{
		// Arrange
		var jsonOptions = Options.Create(new JsonOptions());
		var serializer = new Serializer(jsonOptions);
		var json = "{\"name\":\"Test\",\"value\":123}";
		var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

		// Act
		var result = await serializer.DeserializeAsync<TestDto>(stream);

		// Assert
		Assert.NotNull(result);
		Assert.Equal("Test", result.Name);
		Assert.Equal(123, result.Value);
	}

	/// <summary>
	/// Verifies that the async stream-based DeserializeAsync method handles invalid JSON appropriately.
	/// This test ensures proper exception handling when malformed JSON is provided via stream in async context.
	/// </summary>
	[Fact]
	public async Task DeserializeAsync_Stream_HandlesInvalidJson_Gracefully()
	{
		// Arrange
		var jsonOptions = Options.Create(new JsonOptions());
		var serializer = new Serializer(jsonOptions);
		var invalidJson = "{invalid json}";
		var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(invalidJson));

		// Act & Assert
		var exception = await Record.ExceptionAsync(() => serializer.DeserializeAsync<TestDto>(stream));

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
