using System;
using System.Net.Http;
using System.Threading.Tasks;
using AsyncEndpoints.Entities;

namespace AsyncEndpoints.Utilities;

/// <summary>
/// Provides methods for classifying exceptions into error types to determine retry behavior.
/// </summary>
public static class ErrorClassifier
{
    /// <summary>
    /// Classifies an exception into an error type that determines how it should be handled.
    /// </summary>
    /// <param name="ex">The exception to classify.</param>
    /// <returns>The <see cref="ErrorType"/> indicating how the error should be treated.</returns>
    public static ErrorType Classify(Exception ex)
    {
        return ex switch
        {
            TaskCanceledException => ErrorType.Transient,
            TimeoutException => ErrorType.Transient,
            HttpRequestException httpEx when httpEx.Message.Contains("timeout") => ErrorType.Transient,
            ArgumentException => ErrorType.Permanent,
            InvalidOperationException => ErrorType.Permanent,
            _ => ErrorType.Retriable
        };
    }
}
