using System;

namespace AsyncEndpoints.Utilities;

public class AsyncEndpointError(string code, string message, Exception? exception = null)
{
    public string Code { get; } = code ?? throw new ArgumentNullException(nameof(code));
    public string Message { get; } = message ?? throw new ArgumentNullException(nameof(message));
    public Exception? Exception { get; } = exception;

    public static AsyncEndpointError FromMessage(string message, Exception? exception = null)
    {
        return new AsyncEndpointError("UNKNOWN", message, exception);
    }

    public static AsyncEndpointError FromCode(string code, string message, Exception? exception = null)
    {
        return new AsyncEndpointError(code, message, exception);
    }

    public static AsyncEndpointError FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return new AsyncEndpointError(exception.GetType().Name.ToUpper(), exception.Message, exception);
    }

    public override string ToString()
    {
        return $"[{Code}] {Message}";
    }
}