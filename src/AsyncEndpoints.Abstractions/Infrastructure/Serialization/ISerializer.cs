namespace AsyncEndpoints.Abstractions.Infrastructure.Serialization;

public interface ISerializer
{
	string Serialize<T>(T value);
	T? Deserialize<T>(string json);
}
