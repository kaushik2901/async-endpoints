using AsyncEndpoints.Entities;
using AsyncEndpoints.Utilities;

namespace AsyncEndpoints.UnitTests;

public class ErrorClassifierTests
{
    [Theory, AutoMoqData]
    public void Classify_ReturnsCorrectErrorType_ForKnownExceptions(
        string message)
    {
        // Act & Assert for different exception types
        var exception1 = new InvalidOperationException(message);
        var error1 = ErrorClassifier.Classify(exception1);
        Assert.Equal(ErrorType.Permanent, error1);

        var exception2 = new ArgumentException(message);
        var error2 = ErrorClassifier.Classify(exception2);
        Assert.Equal(ErrorType.Permanent, error2);

        var exception3 = new TimeoutException(message);
        var error3 = ErrorClassifier.Classify(exception3);
        Assert.Equal(ErrorType.Transient, error3);

        var exception4 = new TaskCanceledException(message);
        var error4 = ErrorClassifier.Classify(exception4);
        Assert.Equal(ErrorType.Transient, error4);
    }

    [Theory, AutoMoqData]
    public void Classify_ReturnsRetriable_ForUnknownExceptions(
        string message)
    {
        // Arrange
        var exception = new CustomException(message);

        // Act
        var error = ErrorClassifier.Classify(exception);

        // Assert
        Assert.Equal(ErrorType.Retriable, error);
    }

    // Private class for testing unknown exceptions
    private class CustomException : Exception
    {
        public CustomException(string message) : base(message) { }
    }
}

public class ExceptionSerializerTests
{
    [Theory, AutoMoqData]
    public void Serialize_ReturnsCorrectString(
        string message)
    {
        // Arrange
        var exception = new InvalidOperationException(message);

        // Act
        var result = ExceptionSerializer.Serialize(exception);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(message, result);
        Assert.Contains(nameof(InvalidOperationException), result);
    }

    [Theory, AutoMoqData]
    public void Serialize_HandlesNestedExceptions(
        string message,
        string innerMessage)
    {
        // Arrange
        var innerException = new ArgumentException(innerMessage);
        var outerException = new InvalidOperationException(message, innerException);

        // Act
        var result = ExceptionSerializer.Serialize(outerException);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(message, result);
        Assert.Contains(innerMessage, result);
        Assert.Contains(nameof(InvalidOperationException), result);
        Assert.Contains(nameof(ArgumentException), result);
    }

    [Theory, AutoMoqData]
    public void Serialize_HandlesNullException()
    {
        // Act
        var result = ExceptionSerializer.Serialize(null!);

        // Assert
        Assert.Equal(string.Empty, result);
    }
}