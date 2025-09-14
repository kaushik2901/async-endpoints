using System.Text.Json.Serialization;

[JsonSerializable(typeof(Todo[]))]
internal partial class AsyncEndpointsJsonSerializerContext : JsonSerializerContext
{

}
