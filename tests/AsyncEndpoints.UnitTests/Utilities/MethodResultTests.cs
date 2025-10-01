using AsyncEndpoints.Utilities;

namespace AsyncEndpoints.UnitTests.Utilities;

public class MethodResultTests
{
	[Fact]
	public void Success_CreatesSuccessfulResult()
	{
		// Act
		var result = MethodResult.Success();

		// Assert
		Assert.True(result.IsSuccess);
		Assert.False(result.IsFailure);
		Assert.NotNull(result.Error); // The error property is initialized even for successful results with a placeholder
		Assert.Null(result.Exception);
	}

	[Fact]
	public void Failure_WithErrorMessage_CreatesFailedResult()
	{
		// Arrange
		var errorMessage = "Test error";

		// Act
		var result = MethodResult.Failure(errorMessage);

		// Assert
		Assert.False(result.IsSuccess);
		Assert.True(result.IsFailure);
		Assert.NotNull(result.Error);
		Assert.Equal("UNKNOWN", result.Error!.Code);
		Assert.Equal(errorMessage, result.Error.Message);
		Assert.Null(result.Exception);
	}

	[Fact]
	public void Failure_WithAsyncEndpointError_CreatesFailedResult()
	{
		// Arrange
		var error = AsyncEndpointError.FromCode("TEST_CODE", "Test error message");

		// Act
		var result = MethodResult.Failure(error);

		// Assert
		Assert.False(result.IsSuccess);
		Assert.True(result.IsFailure);
		Assert.Same(error, result.Error);
		Assert.Equal("TEST_CODE", result.Error!.Code);
		Assert.Equal("Test error message", result.Error.Message);
	}

	[Fact]
	public void Failure_WithException_CreatesFailedResult()
	{
		// Arrange
		var exception = new InvalidOperationException("Test exception");

		// Act
		var result = MethodResult.Failure(exception);

		// Assert
		Assert.False(result.IsSuccess);
		Assert.True(result.IsFailure);
		Assert.NotNull(result.Error);
		Assert.Equal("INVALIDOPERATIONEXCEPTION", result.Error!.Code);
		Assert.Equal("Test exception", result.Error.Message);
		Assert.Same(exception, result.Exception);
	}
}

public class MethodResultGenericTests
{
	[Fact]
	public void Success_WithData_CreatesSuccessfulResult()
	{
		// Arrange
		var testData = "Test data";

		// Act
		var result = MethodResult<string>.Success(testData);

		// Assert
		Assert.True(result.IsSuccess);
		Assert.False(result.IsFailure);
		Assert.NotNull(result.Error); // The error property is initialized even for successful results with a placeholder
		Assert.Null(result.Exception);
		Assert.Equal(testData, result.Data);
	}

	[Fact]
	public void Failure_WithErrorMessage_CreatesFailedResult()
	{
		// Arrange
		var errorMessage = "Test error";

		// Act
		var result = MethodResult<string>.Failure(errorMessage);

		// Assert
		Assert.False(result.IsSuccess);
		Assert.True(result.IsFailure);
		Assert.NotNull(result.Error);
		Assert.Equal("UNKNOWN", result.Error!.Code);
		Assert.Equal(errorMessage, result.Error.Message);
		Assert.Null(result.Data);
	}

	[Fact]
	public void Failure_WithAsyncEndpointError_CreatesFailedResult()
	{
		// Arrange
		var error = AsyncEndpointError.FromCode("TEST_CODE", "Test error message");

		// Act
		var result = MethodResult<string>.Failure(error);

		// Assert
		Assert.False(result.IsSuccess);
		Assert.True(result.IsFailure);
		Assert.Same(error, result.Error);
		Assert.Equal("TEST_CODE", result.Error!.Code);
		Assert.Equal("Test error message", result.Error.Message);
		Assert.Null(result.Data);
	}

	[Fact]
	public void Failure_WithException_CreatesFailedResult()
	{
		// Arrange
		var exception = new InvalidOperationException("Test exception");

		// Act
		var result = MethodResult<string>.Failure(exception);

		// Assert
		Assert.False(result.IsSuccess);
		Assert.True(result.IsFailure);
		Assert.NotNull(result.Error);
		Assert.Equal("INVALIDOPERATIONEXCEPTION", result.Error!.Code);
		Assert.Equal("Test exception", result.Error.Message);
		Assert.Same(exception, result.Exception);
		Assert.Null(result.Data);
	}
}