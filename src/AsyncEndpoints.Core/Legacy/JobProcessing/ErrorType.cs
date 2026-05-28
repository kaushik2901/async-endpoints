namespace AsyncEndpoints.Core.Legacy.JobProcessing;

/// <summary>
/// Represents the type of error that occurred during job processing.
/// </summary>
[Obsolete("No longer used in the new pipeline; exceptions are handled directly.")]
public enum ErrorType
{
	/// <summary>
	/// Network timeouts, temporary service unavailability - these errors may succeed on retry.
	/// </summary>
	Transient = 100,

	/// <summary>
	/// Invalid arguments, business logic errors - these errors will likely fail on retry.
	/// </summary>
	Permanent = 200,

	/// <summary>
	/// Unknown errors that might succeed on retry.
	/// </summary>
	Retriable = 300
}
