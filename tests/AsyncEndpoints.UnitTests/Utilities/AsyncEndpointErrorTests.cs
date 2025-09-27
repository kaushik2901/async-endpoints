using AsyncEndpoints.UnitTests.TestSupport;
using AsyncEndpoints.Utilities;

namespace AsyncEndpoints.UnitTests.Utilities;

public class AsyncEndpointErrorTests
{
    [Theory, AutoMoqData]
    public void Constructor_SetsPropertiesCorrectly(
        string code,
        string message,
        InvalidOperationException exception)
    {
        // Act
        var error = new AsyncEndpointError(code, message, exception);

        // Assert
        Assert.Equal(code, error.Code);
        Assert.Equal(message, error.Message);
        Assert.Same(exception, error.Exception);
    }

    [Theory, AutoMoqData]
    public void Constructor_WithNullCode_ThrowsArgumentNullException(
        string message,
        InvalidOperationException exception)
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AsyncEndpointError(null!, message, exception));
    }

    [Theory, AutoMoqData]
    public void Constructor_WithNullMessage_ThrowsArgumentNullException(
        string code,
        InvalidOperationException exception)
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AsyncEndpointError(code, null!, exception));
    }

    [Theory, AutoMoqData]
    public void FromMessage_CreatesErrorWithUnknownCode(
        string message,
        InvalidOperationException exception)
    {
        // Act
        var error = AsyncEndpointError.FromMessage(message, exception);

        // Assert
        Assert.Equal("UNKNOWN", error.Code);
        Assert.Equal(message, error.Message);
        Assert.Same(exception, error.Exception);
    }

    [Theory, AutoMoqData]
    public void FromMessage_WithoutException_CreatesError(
        string message)
    {
        // Act
        var error = AsyncEndpointError.FromMessage(message);

        // Assert
        Assert.Equal("UNKNOWN", error.Code);
        Assert.Equal(message, error.Message);
        Assert.Null(error.Exception);
    }

    [Theory, AutoMoqData]
    public void FromCode_CreatesErrorWithSpecifiedCode(
        string code,
        string message,
        InvalidOperationException exception)
    {
        // Act
        var error = AsyncEndpointError.FromCode(code, message, exception);

        // Assert
        Assert.Equal(code, error.Code);
        Assert.Equal(message, error.Message);
        Assert.Same(exception, error.Exception);
    }

    [Theory, AutoMoqData]
    public void FromCode_WithoutException_CreatesError(
        string code,
        string message)
    {
        // Act
        var error = AsyncEndpointError.FromCode(code, message);

        // Assert
        Assert.Equal(code, error.Code);
        Assert.Equal(message, error.Message);
        Assert.Null(error.Exception);
    }

    [Theory, AutoMoqData]
    public void FromException_CreatesErrorFromException(
        InvalidOperationException exception)
    {
        // Act
        var error = AsyncEndpointError.FromException(exception);

        // Assert
        Assert.Equal("INVALIDOPERATIONEXCEPTION", error.Code);
        Assert.Equal(exception.Message, error.Message);
        Assert.Same(exception, error.Exception);
    }

    [Fact]
    public void FromException_WithNullException_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => AsyncEndpointError.FromException(null!));
    }

    [Theory, AutoMoqData]
    public void ToString_ReturnsFormattedString(
        string code,
        string message)
    {
        // Arrange
        var error = new AsyncEndpointError(code, message);

        // Act
        var result = error.ToString();

        // Assert
        Assert.Equal($"[{code}] {message}", result);
    }
}