using System;
using System.Net.Http;
using System.Threading.Tasks;
using AsyncEndpoints.Entities;

namespace AsyncEndpoints.Utilities;

public static class ErrorClassifier
{
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
