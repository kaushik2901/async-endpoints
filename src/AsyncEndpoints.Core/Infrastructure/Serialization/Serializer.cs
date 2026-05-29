using AsyncEndpoints.Abstractions.Infrastructure.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace AsyncEndpoints.Core.Infrastructure.Serialization;

public class Serializer : ISerializer
{
	private readonly JsonSerializerContext[] _contexts;

	public Serializer(IEnumerable<JsonSerializerContext> contexts)
	{
		_contexts = contexts.ToArray();
	}

	public string Serialize<T>(T value)
	{
		var typeInfo = ResolveTypeInfo<T>();
		return JsonSerializer.Serialize(value, typeInfo);
	}

	public T? Deserialize<T>(string json)
	{
		var typeInfo = ResolveTypeInfo<T>();
		return JsonSerializer.Deserialize(json, typeInfo);
	}

	private JsonTypeInfo<T> ResolveTypeInfo<T>()
	{
		foreach (var context in _contexts)
		{
			var typeInfo = context.GetTypeInfo(typeof(T));
			if (typeInfo is JsonTypeInfo<T> typedInfo)
			{
				return typedInfo;
			}
		}

		throw new KeyNotFoundException($"No JsonSerializerContext registered for type '{typeof(T).FullName}'.");
	}
}
