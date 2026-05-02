namespace AsyncEndpoints.Core.Serialization;

public class JobSerializationException : Exception
{
	public JobSerializationException(string message, Exception? innerException = null)
		: base(message, innerException) { }
}

public class JobDeserializationException : Exception
{
	public JobDeserializationException(string message, Exception? innerException = null)
		: base(message, innerException) { }
}

public class UnknownJobTypeException : Exception
{
	public UnknownJobTypeException(string typeName)
		: base($"Unknown job type: {typeName}") { }
}
