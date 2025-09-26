using AsyncEndpoints.Utilities;

namespace AsyncEndpoints.UnitTests;

public class AsyncEndpointErrorTests
{
    [Fact]
    public void Constructor_SetsPropertiesCorrectly()
    {
        // Arrange
        var code = "TEST_CODE";
        var message = "Test error message";
        var exception = new InvalidOperationException("Inner exception");

        // Act
        var error = new AsyncEndpointError(code, message, exception);

        // Assert
        Assert.Equal(code, error.Code);
        Assert.Equal(message, error.Message);
        Assert.Same(exception, error.Exception);
    }

    [Fact]
    public void Constructor_WithNullCode_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AsyncEndpointError(null!, "message"));
    }

    [Fact]
    public void Constructor_WithNullMessage_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AsyncEndpointError("code", null!));
    }

    [Fact]
    public void FromMessage_CreatesErrorWithUnknownCode()
    {
        // Arrange
        var message = "Test message";
        var exception = new InvalidOperationException("Inner exception");

        // Act
        var error = AsyncEndpointError.FromMessage(message, exception);

        // Assert
        Assert.Equal("UNKNOWN", error.Code);
        Assert.Equal(message, error.Message);
        Assert.Same(exception, error.Exception);
    }

    [Fact]
    public void FromMessage_WithoutException_CreatesError()
    {
        // Arrange
        var message = "Test message";

        // Act
        var error = AsyncEndpointError.FromMessage(message);

        // Assert
        Assert.Equal("UNKNOWN", error.Code);
        Assert.Equal(message, error.Message);
        Assert.Null(error.Exception);
    }

    [Fact]
    public void FromCode_CreatesErrorWithSpecifiedCode()
    {
        // Arrange
        var code = "CUSTOM_CODE";
        var message = "Test message";
        var exception = new InvalidOperationException("Inner exception");

        // Act
        var error = AsyncEndpointError.FromCode(code, message, exception);

        // Assert
        Assert.Equal(code, error.Code);
        Assert.Equal(message, error.Message);
        Assert.Same(exception, error.Exception);
    }

    [Fact]
    public void FromCode_WithoutException_CreatesError()
    {
        // Arrange
        var code = "CUSTOM_CODE";
        var message = "Test message";

        // Act
        var error = AsyncEndpointError.FromCode(code, message);

        // Assert
        Assert.Equal(code, error.Code);
        Assert.Equal(message, error.Message);
        Assert.Null(error.Exception);
    }

    [Fact]
    public void FromException_CreatesErrorFromException()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception message");

        // Act
        var error = AsyncEndpointError.FromException(exception);

        // Assert
        Assert.Equal("INVALIDOPERATIONEXCEPTION", error.Code);
        Assert.Equal("Test exception message", error.Message);
        Assert.Same(exception, error.Exception);
    }

    [Fact]
    public void FromException_WithNullException_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => AsyncEndpointError.FromException(null!));
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var error = new AsyncEndpointError("TEST_CODE", "Test message");

        // Act
        var result = error.ToString();

        // Assert
        Assert.Equal("[TEST_CODE] Test message", result);
    }

    [Fact]
    public void ToString_WithException_ReturnsFormattedString()
    {
        // Arrange
        var exception = new InvalidOperationException("Inner exception");
        var error = new AsyncEndpointError("TEST_CODE", "Test message", exception);

        // Act
        var result = error.ToString();

        // Assert
        Assert.Equal("[TEST_CODE] Test message", result);
    }
}