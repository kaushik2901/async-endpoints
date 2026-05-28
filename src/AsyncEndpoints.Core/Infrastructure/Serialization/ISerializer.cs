using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace AsyncEndpoints.Core.Infrastructure.Serialization;

/// <summary>
/// Provides methods for serializing and deserializing objects to and from JSON.
/// </summary>
public interface ISerializer
{
	/// <summary>
	/// Serializes an object to JSON string using source-generated JsonTypeInfo (AOT-safe).
	/// </summary>
	string Serialize<T>(T value, JsonTypeInfo<T> jsonTypeInfo);

	/// <summary>
	/// Serializes an object to JSON string.
	/// </summary>
	[RequiresUnreferencedCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	[RequiresDynamicCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	string Serialize<T>(T value, JsonSerializerOptions? options);

	/// <summary>
	/// Serializes an object to JSON string using source-generated JsonTypeInfo (AOT-safe).
	/// </summary>
	string Serialize(object value, Type type, JsonTypeInfo jsonTypeInfo);

	/// <summary>
	/// Serializes an object to JSON string.
	/// </summary>
	[RequiresUnreferencedCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	[RequiresDynamicCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	string Serialize(object value, Type type, JsonSerializerOptions? options);

	/// <summary>
	/// Deserializes a JSON string using source-generated JsonTypeInfo (AOT-safe).
	/// </summary>
	T? Deserialize<T>(string json, JsonTypeInfo<T> jsonTypeInfo);

	/// <summary>
	/// Deserializes a JSON string to an object of type T.
	/// </summary>
	[RequiresUnreferencedCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	[RequiresDynamicCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	T? Deserialize<T>(string json, JsonSerializerOptions? options);

	/// <summary>
	/// Deserializes a JSON string using source-generated JsonTypeInfo (AOT-safe).
	/// </summary>
	object? Deserialize(string json, Type type, JsonTypeInfo jsonTypeInfo);

	/// <summary>
	/// Deserializes a JSON string to an object of the specified type.
	/// </summary>
	[RequiresUnreferencedCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	[RequiresDynamicCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	object? Deserialize(string json, Type type, JsonSerializerOptions? options);

	/// <summary>
	/// Deserializes a JSON stream using source-generated JsonTypeInfo (AOT-safe).
	/// </summary>
	T? Deserialize<T>(Stream stream, JsonTypeInfo<T> jsonTypeInfo);

	/// <summary>
	/// Deserializes a JSON stream to an object of type T.
	/// </summary>
	[RequiresUnreferencedCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	[RequiresDynamicCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	T? Deserialize<T>(Stream stream, JsonSerializerOptions? options);

	/// <summary>
	/// Deserializes a JSON stream using source-generated JsonTypeInfo (AOT-safe).
	/// </summary>
	object? Deserialize(Stream stream, Type type, JsonTypeInfo jsonTypeInfo);

	/// <summary>
	/// Deserializes a JSON stream to an object of the specified type.
	/// </summary>
	[RequiresUnreferencedCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	[RequiresDynamicCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	object? Deserialize(Stream stream, Type type, JsonSerializerOptions? options);

	/// <summary>
	/// Deserializes a JSON stream using source-generated JsonTypeInfo (AOT-safe).
	/// </summary>
	Task<T?> DeserializeAsync<T>(Stream stream, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken = default);

	/// <summary>
	/// Deserializes a JSON stream to an object of type T asynchronously.
	/// </summary>
	[RequiresUnreferencedCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	[RequiresDynamicCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	Task<T?> DeserializeAsync<T>(Stream stream, JsonSerializerOptions? options, CancellationToken cancellationToken = default);

	/// <summary>
	/// Deserializes a JSON stream using source-generated JsonTypeInfo (AOT-safe).
	/// </summary>
	Task<object?> DeserializeAsync(Stream stream, Type type, JsonTypeInfo jsonTypeInfo, CancellationToken cancellationToken = default);

	/// <summary>
	/// Deserializes a JSON stream to an object of the specified type asynchronously.
	/// </summary>
	[RequiresUnreferencedCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	[RequiresDynamicCode("Use the JsonTypeInfo overload for AOT compatibility.")]
	Task<object?> DeserializeAsync(Stream stream, Type type, JsonSerializerOptions? options, CancellationToken cancellationToken = default);
}
