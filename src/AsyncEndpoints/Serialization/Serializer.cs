using System;
using System.Text.Json;
using AsyncEndpoints.Contracts;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.Serialization;

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
        return JsonSerializer.Serialize(value, serializerOptions);
    }

    /// <inheritdoc />
    public string Serialize(object value, Type type, JsonSerializerOptions? options = null)
    {
        var serializerOptions = options ?? _jsonOptions.SerializerOptions;
        return JsonSerializer.Serialize(value, type, serializerOptions);
    }

    /// <inheritdoc />
    public T? Deserialize<T>(string json, JsonSerializerOptions? options = null)
    {
        var serializerOptions = options ?? _jsonOptions.SerializerOptions;
        return JsonSerializer.Deserialize<T>(json, serializerOptions);
    }

    /// <inheritdoc />
    public object? Deserialize(string json, Type type, JsonSerializerOptions? options = null)
    {
        var serializerOptions = options ?? _jsonOptions.SerializerOptions;
        return JsonSerializer.Deserialize(json, type, serializerOptions);
    }
}