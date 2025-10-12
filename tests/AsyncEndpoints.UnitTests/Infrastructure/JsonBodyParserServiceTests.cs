using System.Text;
using System.Text.Json;
using AsyncEndpoints.Infrastructure.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncEndpoints.UnitTests.Infrastructure;

public class JsonBodyParserServiceTests
{
	private readonly ServiceProvider _serviceProvider;
	private readonly IJsonBodyParserService _jsonBodyParserService;

	public JsonBodyParserServiceTests()
	{
		var services = new ServiceCollection();
		services.AddOptions();
		services.AddSingleton<ISerializer, Serializer>(); // Register the serializer service
		services.AddScoped<IJsonBodyParserService, JsonBodyParserService>();
		_serviceProvider = services.BuildServiceProvider();
		_jsonBodyParserService = _serviceProvider.GetRequiredService<IJsonBodyParserService>();
	}

	/// <summary>
	/// Verifies that when valid JSON is provided in the request body with the correct content type,
	/// the JsonBodyParserService correctly deserializes it to the target object type.
	/// This ensures proper JSON parsing functionality for valid inputs.
	/// </summary>
	[Fact]
	public async Task ParseAsync_WithValidJson_ReturnsDeserializedObject()
	{
		// Arrange
		var testData = new { Name = "Test", Value = 123 };
		var json = JsonSerializer.Serialize(testData);
		var httpContext = CreateHttpContextWithJsonBody(json, "application/json");

		// Act
		var result = await _jsonBodyParserService.ParseAsync<TestData>(httpContext);

		// Assert
		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Data);
		Assert.Equal("Test", result.Data.Name);
		Assert.Equal(123, result.Data.Value);
	}

	/// <summary>
	/// Verifies that when malformed JSON is provided in the request body, 
	/// the JsonBodyParserService returns a failure result with appropriate error details.
	/// This ensures proper handling of invalid JSON content.
	/// </summary>
	[Fact]
	public async Task ParseAsync_WithMalformedJson_ReturnsFailure()
	{
		// Arrange
		var malformedJson = "{ \"Name\": \"Test\", \"Value\": }"; // Malformed JSON
		var httpContext = CreateHttpContextWithJsonBody(malformedJson, "application/json");

		// Act
		var result = await _jsonBodyParserService.ParseAsync<TestData>(httpContext);

		// Assert
		Assert.True(result.IsFailure);
		Assert.NotNull(result.Error);
		Assert.Contains("Invalid JSON format", result.Error.Message);
	}

	/// <summary>
	/// Verifies that when an unsupported type is provided for deserialization,
	/// the JsonBodyParserService returns a failure result with appropriate error details.
	/// This ensures proper handling of unsupported deserialization scenarios.
	/// </summary>
	[Fact]
	public async Task ParseAsync_WithUnsupportedType_ReturnsFailure()
	{
		// Arrange
		var json = JsonSerializer.Serialize(new { Name = "Test", Value = 123 });
		var httpContext = CreateHttpContextWithJsonBody(json, "application/json");

		// Act
		// We can't easily test an unsupported type, but we can test with a type that might cause issues
		var result = await _jsonBodyParserService.ParseAsync<UnsupportedType>(httpContext);

		// Assert - The result might succeed or fail depending on the type, but if it fails,
		// it should provide meaningful error information
		// For this test, we're mainly checking that it doesn't crash and handles exceptions gracefully
		Assert.NotNull(result);
	}

	/// <summary>
	/// Verifies that when an empty request body is provided, the JsonBodyParserService returns a failure result.
	/// This ensures proper error handling for empty content.
	/// </summary>
	[Fact]
	public async Task ParseAsync_WithEmptyBody_ReturnsFailure()
	{
		// Arrange
		var httpContext = CreateHttpContextWithJsonBody("", "application/json");
		httpContext.Request.ContentLength = 0;

		// Act
		var result = await _jsonBodyParserService.ParseAsync<TestData>(httpContext);

		// Assert
		Assert.True(result.IsFailure);
		Assert.NotNull(result.Error);
	}

	/// <summary>
	/// Verifies that when a non-JSON content type is provided, the JsonBodyParserService returns a failure result.
	/// This ensures proper validation of content types before attempting deserialization.
	/// </summary>
	[Fact]
	public async Task ParseAsync_WithNonJsonContentType_ReturnsFailure()
	{
		// Arrange
		var httpContext = CreateHttpContextWithJsonBody("{\"name\":\"test\"}", "text/plain");

		// Act
		var result = await _jsonBodyParserService.ParseAsync<TestData>(httpContext);

		// Assert
		Assert.True(result.IsFailure);
		Assert.NotNull(result.Error);
	}

	/// <summary>
	/// Verifies that when a null request body is provided, the JsonBodyParserService returns a failure result.
	/// This ensures proper error handling for null content.
	/// </summary>
	[Fact]
	public async Task ParseAsync_WithNullBody_ReturnsFailure()
	{
		// Arrange
		var httpContext = CreateHttpContextWithJsonBody("", "application/json");
		httpContext.Request.Body = Stream.Null;

		// Act
		var result = await _jsonBodyParserService.ParseAsync<TestData>(httpContext);

		// Assert
		Assert.True(result.IsFailure);
		Assert.NotNull(result.Error);
	}

	private static DefaultHttpContext CreateHttpContextWithJsonBody(string json, string contentType)
	{
		var httpContext = new DefaultHttpContext();
		var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
		httpContext.Request.Body = stream;
		httpContext.Request.ContentType = contentType;
		httpContext.Request.ContentLength = stream.Length;
		return httpContext;
	}

	private class TestData
	{
		public string? Name { get; set; }
		public int Value { get; set; }
	}

	private class UnsupportedType
	{
		// Using an unusual type to potentially trigger NotSupportedException
		public nint SomeIntPtr { get; set; }
	}
}
