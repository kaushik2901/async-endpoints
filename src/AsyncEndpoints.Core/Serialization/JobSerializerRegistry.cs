using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace AsyncEndpoints.Core.Serialization;

/// <summary>
/// AOT-safe registry for job serializers using JsonTypeInfo.
/// </summary>
public class JobSerializerRegistry
{
	private readonly Dictionary<string, JsonTypeInfo> _typeInfos = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Registers a job type and its associated JsonTypeInfo.
	/// </summary>
	public void Register<T>(JsonTypeInfo<T> typeInfo)
	{
		var key = typeof(T).Name;
		_typeInfos[key] = typeInfo;
	}

	/// <summary>
	/// Serializes a job payload using its registered JsonTypeInfo.
	/// </summary>
	public string Serialize<T>(T payload)
	{
		var key = typeof(T).Name;
		if (!_typeInfos.TryGetValue(key, out var typeInfo) || typeInfo is not JsonTypeInfo<T> typedInfo)
		{
			throw new UnknownJobTypeException(key);
		}

		try
		{
			return JsonSerializer.Serialize(payload, typedInfo);
		}
		catch (Exception ex)
		{
			throw new JobSerializationException($"Failed to serialize job of type {key}", ex);
		}
	}

	/// <summary>
	/// Deserializes a job payload using the registered JsonTypeInfo for the given type name.
	/// </summary>
	public object Deserialize(string typeName, string json)
	{
		if (!_typeInfos.TryGetValue(typeName, out var typeInfo))
		{
			throw new UnknownJobTypeException(typeName);
		}

		try
		{
			return JsonSerializer.Deserialize(json, typeInfo)
				?? throw new JobDeserializationException($"Deserialization returned null for type {typeName}");
		}
		catch (Exception ex) when (ex is not JobDeserializationException)
		{
			throw new JobDeserializationException($"Failed to deserialize job of type {typeName}", ex);
		}
	}

	/// <summary>
	/// Checks if a job type is registered.
	/// </summary>
	public bool IsRegistered(string typeName) => _typeInfos.ContainsKey(typeName);
}
