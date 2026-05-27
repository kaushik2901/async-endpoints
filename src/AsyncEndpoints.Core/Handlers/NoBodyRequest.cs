namespace AsyncEndpoints.Handlers;

/// <summary>
/// A placeholder class representing an empty request body for endpoints that don't require request data.
/// </summary>
public class NoBodyRequest
{
	// This class serves as a placeholder for endpoints without request body
	// It contains no properties as no body data is expected

	public static NoBodyRequest CreateInstance() => new();
}
