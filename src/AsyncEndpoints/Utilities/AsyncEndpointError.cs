using System;

namespace AsyncEndpoints.Utilities;

/// <summary>
/// Represents an error that occurred during async endpoint processing.
/// </summary>
public sealed class AsyncEndpointError(string code, string message, Exception? exception = null)
{
	/// <summary>
	/// Gets the error code.
	/// </summary>
	public string Code { get; } = code ?? throw new ArgumentNullException(nameof(code));

	/// <summary>
	/// Gets the error message.
	/// </summary>
	public string Message { get; } = message ?? throw new ArgumentNullException(nameof(message));

	/// <summary>
	/// Gets the underlying exception, if any.
	/// </summary>
	public Exception? Exception { get; } = exception;

	/// <summary>
	/// Creates an AsyncEndpointError from a message.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="exception">The underlying exception, if any.</param>
	/// <returns>A new <see cref="AsyncEndpointError"/> instance.</returns>
	public static AsyncEndpointError FromMessage(string message, Exception? exception = null)
	{
		return new AsyncEndpointError("UNKNOWN", message, exception);
	}

	/// <summary>
	/// Creates an AsyncEndpointError from a code and message.
	/// </summary>
	/// <param name="code">The error code.</param>
	/// <param name="message">The error message.</param>
	/// <param name="exception">The underlying exception, if any.</param>
	/// <returns>A new <see cref="AsyncEndpointError"/> instance.</returns>
	public static AsyncEndpointError FromCode(string code, string message, Exception? exception = null)
	{
		return new AsyncEndpointError(code, message, exception);
	}

	/// <summary>
	/// Creates an AsyncEndpointError from an exception.
	/// </summary>
	/// <param name="exception">The exception to create the error from.</param>
	/// <returns>A new <see cref="AsyncEndpointError"/> instance.</returns>
	public static AsyncEndpointError FromException(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);

		return new AsyncEndpointError(exception.GetType().Name.ToUpper(), exception.Message, exception);
	}

	/// <summary>
	/// Returns a string representation of the error in the format "[Code] Message".
	/// </summary>
	/// <returns>A string representation of the error.</returns>
	public override string ToString()
	{
		return $"[{Code}] {Message}";
	}
}