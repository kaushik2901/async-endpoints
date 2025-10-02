using System;

namespace AsyncEndpoints.Utilities;

/// <summary>
/// Represents an error that occurred during async endpoint processing.
/// </summary>
public sealed class AsyncEndpointError
{
	/// <summary>
	/// Gets the error code.
	/// </summary>
	public string Code { get; init; } = string.Empty;

	/// <summary>
	/// Gets the error message.
	/// </summary>
	public string Message { get; init; } = string.Empty;

	/// <summary>
	/// Gets the underlying exception information, if any.
	/// </summary>
	public ExceptionInfo? Exception { get; init; }

	/// <summary>
	/// Parameterless constructor for JSON deserialization.
	/// </summary>
	public AsyncEndpointError()
	{
	}

	/// <summary>
	/// Initializes a new instance of the AsyncEndpointError class from an Exception.
	/// </summary>
	/// <param name="code">The error code.</param>
	/// <param name="message">The error message.</param>
	/// <param name="exception">The underlying exception, if any.</param>
	public AsyncEndpointError(string code, string message, Exception? exception = null)
	{
		Code = code ?? throw new ArgumentNullException(nameof(code));
		Message = message ?? throw new ArgumentNullException(nameof(message));
		Exception = exception is not null ? ExceptionInfo.FromException(exception) : null;
	}

	/// <summary>
	/// Initializes a new instance of the AsyncEndpointError class from ExceptionInfo (for internal use).
	/// </summary>
	/// <param name="code">The error code.</param>
	/// <param name="message">The error message.</param>
	/// <param name="exceptionInfo">The underlying exception information, if any.</param>
	private AsyncEndpointError(string code, string message, ExceptionInfo? exceptionInfo)
	{
		Code = code;
		Message = message;
		Exception = exceptionInfo;
	}

	/// <summary>
	/// Creates an AsyncEndpointError from a message.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="exception">The underlying exception, if any.</param>
	/// <returns>A new <see cref="AsyncEndpointError"/> instance.</returns>
	public static AsyncEndpointError FromMessage(string message, Exception? exception = null)
	{
		var exceptionInfo = exception is not null ? ExceptionInfo.FromException(exception) : null;
		return new AsyncEndpointError("UNKNOWN", message, exceptionInfo);
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
		var exceptionInfo = exception is not null ? ExceptionInfo.FromException(exception) : null;
		return new AsyncEndpointError(code, message, exceptionInfo);
	}

	/// <summary>
	/// Creates an AsyncEndpointError from an exception.
	/// </summary>
	/// <param name="exception">The exception to create the error from.</param>
	/// <returns>A new <see cref="AsyncEndpointError"/> instance.</returns>
	public static AsyncEndpointError FromException(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);

		var exceptionInfo = ExceptionInfo.FromException(exception);
		return new AsyncEndpointError(exception.GetType().Name.ToUpper(), exception.Message, exceptionInfo);
	}
}