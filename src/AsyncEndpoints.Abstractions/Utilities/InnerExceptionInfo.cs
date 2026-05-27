namespace AsyncEndpoints.Utilities;

/// <summary>
/// Contains serializable information about an inner exception.
/// </summary>
public sealed class InnerExceptionInfo
{
	/// <summary>
	/// Gets the inner exception type name, if any.
	/// </summary>
	public string? Type { get; init; }

	/// <summary>
	/// Gets the inner exception message, if any.
	/// </summary>
	public string? Message { get; init; }

	/// <summary>
	/// Gets the stack trace of the inner exception, if any.
	/// </summary>
	public string? StackTrace { get; init; }
}
