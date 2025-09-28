using System;
using System.Text.Json;

namespace AsyncEndpoints.Contracts;

/// <summary>
/// Provides methods for serializing and deserializing objects to and from JSON.
/// </summary>
public interface ISerializer
{
    /// <summary>
    /// Serializes an object to JSON string.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <param name="options">Optional JsonSerializerOptions to use for serialization.</param>
    /// <returns>A JSON string representation of the object.</returns>
    string Serialize<T>(T value, JsonSerializerOptions? options = null);

    /// <summary>
    /// Serializes an object to JSON string.
    /// </summary>
    /// <param name="value">The object to serialize.</param>
    /// <param name="type">The type of the object to deserialize.</param>
    /// <param name="options">Optional JsonSerializerOptions to use for serialization.</param>
    /// <returns>A JSON string representation of the object.</returns>
    string Serialize(object value, Type type, JsonSerializerOptions? options = null);

    /// <summary>
    /// Deserializes a JSON string to an object of type T.
    /// </summary>
    /// <typeparam name="T">The type of the object to deserialize.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="options">Optional JsonSerializerOptions to use for deserialization.</param>
    /// <returns>The deserialized object.</returns>
    T? Deserialize<T>(string json, JsonSerializerOptions? options = null);

    /// <summary>
    /// Deserializes a JSON string to an object of the specified type.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="type">The type of the object to deserialize.</param>
    /// <param name="options">Optional JsonSerializerOptions to use for deserialization.</param>
    /// <returns>The deserialized object.</returns>
    object? Deserialize(string json, Type type, JsonSerializerOptions? options = null);
}