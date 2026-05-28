using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace AsyncEndpoints.Core.Infrastructure.Serialization;

public class Serializer(JsonSerializerOptions? jsonOptions = null) : ISerializer
{
	private readonly JsonSerializerOptions _jsonOptions = jsonOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);

	private static readonly string _serializationErrorMessage = "Error reading from stream during deserialization";

	public string Serialize<T>(T value, JsonTypeInfo<T> jsonTypeInfo)
	{
		return JsonSerializer.Serialize(value, jsonTypeInfo);
	}

	[RequiresUnreferencedCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	[RequiresDynamicCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	public string Serialize<T>(T value, JsonSerializerOptions? options)
	{
		var serializerOptions = options ?? _jsonOptions;
		return JsonSerializer.Serialize(value, serializerOptions);
	}

	public string Serialize(object value, Type type, JsonTypeInfo jsonTypeInfo)
	{
		return JsonSerializer.Serialize(value, jsonTypeInfo);
	}

	[RequiresUnreferencedCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	[RequiresDynamicCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	public string Serialize(object value, Type type, JsonSerializerOptions? options)
	{
		var serializerOptions = options ?? _jsonOptions;
		return JsonSerializer.Serialize(value, type, serializerOptions);
	}

	public T? Deserialize<T>(string json, JsonTypeInfo<T> jsonTypeInfo)
	{
		return JsonSerializer.Deserialize(json, jsonTypeInfo);
	}

	[RequiresUnreferencedCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	[RequiresDynamicCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	public T? Deserialize<T>(string json, JsonSerializerOptions? options)
	{
		var serializerOptions = options ?? _jsonOptions;
		return JsonSerializer.Deserialize<T>(json, serializerOptions);
	}

	public object? Deserialize(string json, Type type, JsonTypeInfo jsonTypeInfo)
	{
		return JsonSerializer.Deserialize(json, jsonTypeInfo);
	}

	[RequiresUnreferencedCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	[RequiresDynamicCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	public object? Deserialize(string json, Type type, JsonSerializerOptions? options)
	{
		var serializerOptions = options ?? _jsonOptions;
		return JsonSerializer.Deserialize(json, type, serializerOptions);
	}

	public T? Deserialize<T>(Stream stream, JsonTypeInfo<T> jsonTypeInfo)
	{
		try
		{
			return JsonSerializer.Deserialize(stream, jsonTypeInfo);
		}
		catch (JsonException)
		{
			if (stream.CanSeek)
			{
				stream.Seek(0, SeekOrigin.Begin);
			}
			throw;
		}
		catch (IOException ioEx)
		{
			throw new InvalidOperationException(_serializationErrorMessage, ioEx);
		}
	}

	[RequiresUnreferencedCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	[RequiresDynamicCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	public T? Deserialize<T>(Stream stream, JsonSerializerOptions? options)
	{
		var serializerOptions = options ?? _jsonOptions;
		try
		{
			return JsonSerializer.Deserialize<T>(stream, serializerOptions);
		}
		catch (JsonException)
		{
			if (stream.CanSeek)
			{
				stream.Seek(0, SeekOrigin.Begin);
			}
			throw;
		}
		catch (IOException ioEx)
		{
			throw new InvalidOperationException(_serializationErrorMessage, ioEx);
		}
	}

	public object? Deserialize(Stream stream, Type type, JsonTypeInfo jsonTypeInfo)
	{
		try
		{
			return JsonSerializer.Deserialize(stream, jsonTypeInfo);
		}
		catch (JsonException)
		{
			if (stream.CanSeek)
			{
				stream.Seek(0, SeekOrigin.Begin);
			}
			throw;
		}
		catch (IOException ioEx)
		{
			throw new InvalidOperationException(_serializationErrorMessage, ioEx);
		}
	}

	[RequiresUnreferencedCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	[RequiresDynamicCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	public object? Deserialize(Stream stream, Type type, JsonSerializerOptions? options)
	{
		var serializerOptions = options ?? _jsonOptions;
		try
		{
			return JsonSerializer.Deserialize(stream, type, serializerOptions);
		}
		catch (JsonException)
		{
			if (stream.CanSeek)
			{
				stream.Seek(0, SeekOrigin.Begin);
			}
			throw;
		}
		catch (IOException ioEx)
		{
			throw new InvalidOperationException(_serializationErrorMessage, ioEx);
		}
	}

	public async Task<T?> DeserializeAsync<T>(Stream stream, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken = default)
	{
		try
		{
			return await JsonSerializer.DeserializeAsync(stream, jsonTypeInfo, cancellationToken);
		}
		catch (JsonException)
		{
			if (stream.CanSeek)
			{
				stream.Seek(0, SeekOrigin.Begin);
			}
			throw;
		}
		catch (IOException ioEx)
		{
			throw new InvalidOperationException(_serializationErrorMessage, ioEx);
		}
	}

	[RequiresUnreferencedCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	[RequiresDynamicCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	public async Task<T?> DeserializeAsync<T>(Stream stream, JsonSerializerOptions? options, CancellationToken cancellationToken = default)
	{
		var serializerOptions = options ?? _jsonOptions;
		try
		{
			return await JsonSerializer.DeserializeAsync<T>(stream, serializerOptions, cancellationToken);
		}
		catch (JsonException)
		{
			if (stream.CanSeek)
			{
				stream.Seek(0, SeekOrigin.Begin);
			}
			throw;
		}
		catch (IOException ioEx)
		{
			throw new InvalidOperationException(_serializationErrorMessage, ioEx);
		}
	}

	public async Task<object?> DeserializeAsync(Stream stream, Type type, JsonTypeInfo jsonTypeInfo, CancellationToken cancellationToken = default)
	{
		try
		{
			return await JsonSerializer.DeserializeAsync(stream, jsonTypeInfo, cancellationToken);
		}
		catch (JsonException)
		{
			if (stream.CanSeek)
			{
				stream.Seek(0, SeekOrigin.Begin);
			}
			throw;
		}
		catch (IOException ioEx)
		{
			throw new InvalidOperationException(_serializationErrorMessage, ioEx);
		}
	}

	[RequiresUnreferencedCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	[RequiresDynamicCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	public async Task<object?> DeserializeAsync(Stream stream, Type type, JsonSerializerOptions? options, CancellationToken cancellationToken = default)
	{
		var serializerOptions = options ?? _jsonOptions;
		try
		{
			return await JsonSerializer.DeserializeAsync(stream, type, serializerOptions, cancellationToken);
		}
		catch (JsonException)
		{
			if (stream.CanSeek)
			{
				stream.Seek(0, SeekOrigin.Begin);
			}
			throw;
		}
		catch (IOException ioEx)
		{
			throw new InvalidOperationException(_serializationErrorMessage, ioEx);
		}
	}
}
