using System;

namespace AsyncEndpoints.Utilities;

/// <summary>
/// Represents the result of a method operation, indicating whether it was successful or failed.
/// </summary>
public class MethodResult
{
    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the error information if the operation failed.
    /// </summary>
    public AsyncEndpointError Error { get; }

    /// <summary>
    /// Gets the exception that occurred during the operation, if any.
    /// </summary>
    public Exception? Exception { get; }

    protected MethodResult()
    {
        IsSuccess = true;
        Error = AsyncEndpointError.FromCode("PLACEHOLDER_ERROR", string.Empty);
    }

    protected MethodResult(AsyncEndpointError error, Exception? exception = null)
    {
        IsSuccess = false;
        Error = error ?? throw new ArgumentNullException(nameof(error));
        Exception = exception;
    }

    /// <summary>
    /// Creates a successful method result.
    /// </summary>
    /// <returns>A successful <see cref="MethodResult"/>.</returns>
    public static MethodResult Success() => new();

    /// <summary>
    /// Creates a failed method result with the specified error.
    /// </summary>
    /// <param name="error">The error information.</param>
    /// <returns>A failed <see cref="MethodResult"/>.</returns>
    public static MethodResult Failure(AsyncEndpointError error) => new(error);

    /// <summary>
    /// Creates a failed method result with the specified error message.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>A failed <see cref="MethodResult"/>.</returns>
    public static MethodResult Failure(string errorMessage) => new(AsyncEndpointError.FromMessage(errorMessage));

    /// <summary>
    /// Creates a failed method result with the specified exception.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <returns>A failed <see cref="MethodResult"/>.</returns>
    public static MethodResult Failure(Exception exception) => new(AsyncEndpointError.FromException(exception), exception);
}

/// <summary>
/// Represents the result of a method operation that returns a value of type T, 
/// indicating whether it was successful or failed.
/// </summary>
/// <typeparam name="T">The type of the data returned by the operation.</typeparam>
public class MethodResult<T> : MethodResult
{
    /// <summary>
    /// Gets the data returned by the operation if it was successful.
    /// </summary>
    public T? Data { get; }

    private MethodResult(T data) : base()
    {
        Data = data;
    }

    private MethodResult(AsyncEndpointError error, Exception? exception = null)
        : base(error, exception)
    {
    }

    /// <summary>
    /// Creates a successful method result with the specified data.
    /// </summary>
    /// <param name="data">The data to return.</param>
    /// <returns>A successful <see cref="MethodResult{T}"/>.</returns>
    public static MethodResult<T> Success(T data) => new(data);

    /// <summary>
    /// Creates a failed method result with the specified error.
    /// </summary>
    /// <param name="error">The error information.</param>
    /// <returns>A failed <see cref="MethodResult{T}"/>.</returns>
    public new static MethodResult<T> Failure(AsyncEndpointError error) => new(error);

    /// <summary>
    /// Creates a failed method result with the specified error message.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>A failed <see cref="MethodResult{T}"/>.</returns>
    public new static MethodResult<T> Failure(string errorMessage) => new(AsyncEndpointError.FromMessage(errorMessage));

    /// <summary>
    /// Creates a failed method result with the specified exception.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <returns>A failed <see cref="MethodResult{T}"/>.</returns>
    public new static MethodResult<T> Failure(Exception exception) => new(AsyncEndpointError.FromException(exception), exception);
}