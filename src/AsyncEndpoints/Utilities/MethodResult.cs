using System;

namespace AsyncEndpoints.Utilities;

public class MethodResult
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public AsyncEndpointError? Error { get; }
    public Exception? Exception { get; }

    protected MethodResult()
    {
        IsSuccess = true;
        Error = null;
    }

    protected MethodResult(AsyncEndpointError error, Exception? exception = null)
    {
        IsSuccess = false;
        Error = error ?? throw new ArgumentNullException(nameof(error));
        Exception = exception;
    }

    public static MethodResult Success() => new();
    public static MethodResult Failure(AsyncEndpointError error) => new(error);
    public static MethodResult Failure(string errorMessage) => new(AsyncEndpointError.FromMessage(errorMessage));
    public static MethodResult Failure(Exception exception) => new(AsyncEndpointError.FromException(exception), exception);
}

public class MethodResult<T> : MethodResult
{
    public T? Data { get; }

    private MethodResult(T data) : base()
    {
        Data = data;
    }

    private MethodResult(AsyncEndpointError error, Exception? exception = null) 
        : base(error, exception)
    {
    }

    public static MethodResult<T> Success(T data) => new(data);
    public new static MethodResult<T> Failure(AsyncEndpointError error) => new(error);
    public new static MethodResult<T> Failure(string errorMessage) => new(AsyncEndpointError.FromMessage(errorMessage));
    public new static MethodResult<T> Failure(Exception exception) => new(AsyncEndpointError.FromException(exception), exception);
}