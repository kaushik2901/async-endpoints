using System;

namespace AsyncEndpoints.Utilities;

/// <summary>
/// Contains serializable information about an exception.
/// </summary>
public sealed class ExceptionInfo
{
	/// <summary>
	/// Gets the exception type name.
	/// </summary>
	public string Type { get; init; } = string.Empty;

	/// <summary>
	/// Gets the exception message.
	/// </summary>
	public string Message { get; init; } = string.Empty;

	/// <summary>
	/// Gets the stack trace of the exception.
	/// </summary>
	public string? StackTrace { get; init; }

	/// <summary>
	/// Gets the inner exception information, if any.
	/// </summary>
	public InnerExceptionInfo? InnerException { get; init; }

	/// <summary>
	/// Creates an ExceptionInfo from an Exception.
	/// </summary>
	/// <param name="exception">The exception to extract information from.</param>
	/// <returns>An ExceptionInfo instance containing the exception's serializable information.</returns>
	public static ExceptionInfo FromException(Exception exception)
	{
		return new ExceptionInfo
		{
			Type = exception.GetType().Name,
			Message = exception.Message ?? string.Empty,
			StackTrace = exception.StackTrace,
			InnerException = exception.InnerException != null ? new InnerExceptionInfo 
			{ 
				Type = exception.InnerException.GetType().Name,
				Message = exception.InnerException.Message ?? string.Empty,
				StackTrace = exception.InnerException.StackTrace
			} : null
		};
	}

	/// <summary>
	/// Parameterless constructor for JSON deserialization.
	/// </summary>
	public ExceptionInfo()
	{
	}
}
