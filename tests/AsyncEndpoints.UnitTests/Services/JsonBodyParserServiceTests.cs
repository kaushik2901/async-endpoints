using System.Text;
using System.Text.Json;
using AsyncEndpoints.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncEndpoints.UnitTests.Services;

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
}
