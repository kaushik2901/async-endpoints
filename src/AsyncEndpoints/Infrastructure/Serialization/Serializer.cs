using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.Infrastructure.Serialization;

/// <summary>
/// Provides JSON serialization and deserialization functionality using System.Text.Json.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="Serializer"/> class.
/// </remarks>
/// <param name="jsonOptions">Optional JsonOptions to use for serialization/deserialization.</param>
public class Serializer(IOptions<JsonOptions> jsonOptions) : ISerializer
{
	private readonly JsonOptions _jsonOptions = jsonOptions.Value;

	/// <inheritdoc />
	public string Serialize<T>(T value, JsonSerializerOptions? options = null)
	{
		var serializerOptions = options ?? _jsonOptions.SerializerOptions;
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
		return JsonSerializer.Serialize(value, serializerOptions);
#pragma warning restore IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
	}

	/// <inheritdoc />
	public string Serialize(object value, Type type, JsonSerializerOptions? options = null)
	{
		var serializerOptions = options ?? _jsonOptions.SerializerOptions;
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
		return JsonSerializer.Serialize(value, type, serializerOptions);
#pragma warning restore IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
	}

	/// <inheritdoc />
	public T? Deserialize<T>(string json, JsonSerializerOptions? options = null)
	{
		var serializerOptions = options ?? _jsonOptions.SerializerOptions;
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
		return JsonSerializer.Deserialize<T>(json, serializerOptions);
#pragma warning restore IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
	}

	/// <inheritdoc />
	public object? Deserialize(string json, Type type, JsonSerializerOptions? options = null)
	{
		var serializerOptions = options ?? _jsonOptions.SerializerOptions;
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
		return JsonSerializer.Deserialize(json, type, serializerOptions);
#pragma warning restore IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
	}

	/// <inheritdoc />
	public T? Deserialize<T>(Stream stream, JsonSerializerOptions? options = null)
	{
		var serializerOptions = options ?? _jsonOptions.SerializerOptions;
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
		try
		{
			return JsonSerializer.Deserialize<T>(stream, serializerOptions);
		}
		catch (JsonException)
		{
			// Reset stream position if possible and re-throw with context
			if (stream.CanSeek)
			{
				stream.Seek(0, SeekOrigin.Begin);
			}
			throw;
		}
		catch (IOException ioEx)
		{
			throw new InvalidOperationException("Error reading from stream during deserialization", ioEx);
		}
#pragma warning restore IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
	}

	/// <inheritdoc />
	public object? Deserialize(Stream stream, Type type, JsonSerializerOptions? options = null)
	{
		var serializerOptions = options ?? _jsonOptions.SerializerOptions;
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
		try
		{
			return JsonSerializer.Deserialize(stream, type, serializerOptions);
		}
		catch (JsonException)
		{
			// Reset stream position if possible and re-throw with context
			if (stream.CanSeek)
			{
				stream.Seek(0, SeekOrigin.Begin);
			}
			throw;
		}
		catch (IOException ioEx)
		{
			throw new InvalidOperationException("Error reading from stream during deserialization", ioEx);
		}
#pragma warning restore IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
	}

	/// <inheritdoc />
	public async Task<T?> DeserializeAsync<T>(Stream stream, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
	{
		var serializerOptions = options ?? _jsonOptions.SerializerOptions;
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
		try
		{
			return await JsonSerializer.DeserializeAsync<T>(stream, serializerOptions, cancellationToken);
		}
		catch (JsonException)
		{
			// Reset stream position if possible and re-throw with context
			if (stream.CanSeek)
			{
				stream.Seek(0, SeekOrigin.Begin);
			}
			throw;
		}
		catch (IOException ioEx)
		{
			throw new InvalidOperationException("Error reading from stream during deserialization", ioEx);
		}
#pragma warning restore IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
	}

	/// <inheritdoc />
	public async Task<object?> DeserializeAsync(Stream stream, Type type, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
	{
		var serializerOptions = options ?? _jsonOptions.SerializerOptions;
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
		try
		{
			return await JsonSerializer.DeserializeAsync(stream, type, serializerOptions, cancellationToken);
		}
		catch (JsonException)
		{
			// Reset stream position if possible and re-throw with context
			if (stream.CanSeek)
			{
				stream.Seek(0, SeekOrigin.Begin);
			}
			throw;
		}
		catch (IOException ioEx)
		{
			throw new InvalidOperationException("Error reading from stream during deserialization", ioEx);
		}
#pragma warning restore IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
	}
}
