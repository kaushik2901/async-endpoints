using AsyncEndpoints.AspNetCore.Serialization;
using AsyncEndpoints.Core.Infrastructure.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;

namespace AsyncEndpoints.AspNetCore.UnitTests.Infrastructure;

public class JsonBodyParserServiceTests
{
	private readonly ServiceProvider _serviceProvider;
	private readonly IJsonBodyParserService _jsonBodyParserService;

	public JsonBodyParserServiceTests()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddOptions();
		services.AddSingleton<ISerializer, Serializer>();
		services.AddScoped<IJsonBodyParserService, JsonBodyParserService>();
		_serviceProvider = services.BuildServiceProvider();
		_jsonBodyParserService = _serviceProvider.GetRequiredService<IJsonBodyParserService>();
	}

	[Fact]
	public async Task ParseAsync_WithValidJson_ReturnsDeserializedObject()
	{
		var testData = new { Name = "Test", Value = 123 };
		var json = JsonSerializer.Serialize(testData);
		var httpContext = CreateHttpContextWithJsonBody(json, "application/json");

		var result = await _jsonBodyParserService.ParseAsync<TestData>(httpContext);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Data);
		Assert.Equal("Test", result.Data.Name);
		Assert.Equal(123, result.Data.Value);
	}

	[Fact]
	public async Task ParseAsync_WithMalformedJson_ReturnsFailure()
	{
		var malformedJson = "{ \"Name\": \"Test\", \"Value\": }";
		var httpContext = CreateHttpContextWithJsonBody(malformedJson, "application/json");

		var result = await _jsonBodyParserService.ParseAsync<TestData>(httpContext);

		Assert.True(result.IsFailure);
		Assert.NotNull(result.Error);
		Assert.Contains("Invalid JSON format", result.Error.Message);
	}

	[Fact]
	public async Task ParseAsync_WithUnsupportedType_ReturnsFailure()
	{
		var json = JsonSerializer.Serialize(new { Name = "Test", Value = 123 });
		var httpContext = CreateHttpContextWithJsonBody(json, "application/json");

		var result = await _jsonBodyParserService.ParseAsync<UnsupportedType>(httpContext);

		Assert.NotNull(result);
	}

	[Fact]
	public async Task ParseAsync_WithEmptyBody_ReturnsFailure()
	{
		var httpContext = CreateHttpContextWithJsonBody("", "application/json");
		httpContext.Request.ContentLength = 0;

		var result = await _jsonBodyParserService.ParseAsync<TestData>(httpContext);

		Assert.True(result.IsFailure);
		Assert.NotNull(result.Error);
	}

	[Fact]
	public async Task ParseAsync_WithNonJsonContentType_ReturnsFailure()
	{
		var httpContext = CreateHttpContextWithJsonBody("{\"name\":\"test\"}", "text/plain");

		var result = await _jsonBodyParserService.ParseAsync<TestData>(httpContext);

		Assert.True(result.IsFailure);
		Assert.NotNull(result.Error);
	}

	[Fact]
	public async Task ParseAsync_WithNullBody_ReturnsFailure()
	{
		var httpContext = CreateHttpContextWithJsonBody("", "application/json");
		httpContext.Request.Body = Stream.Null;

		var result = await _jsonBodyParserService.ParseAsync<TestData>(httpContext);

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
		public nint SomeIntPtr { get; set; }
	}
}
