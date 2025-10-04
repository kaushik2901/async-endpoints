using AsyncEndpoints.Utilities;

namespace AsyncEndpoints.UnitTests.Utilities;

public class MethodResultTests
{
	[Fact]
	public void MethodResult_Success_ReturnsSuccessResult()
	{
		// Act
		var result = MethodResult.Success();

		// Assert
		Assert.True(result.IsSuccess);
		Assert.False(result.IsFailure);
		Assert.NotNull(result.Error);
	}

	[Fact]
	public void MethodResult_Failure_WithErrorMessage_ReturnsFailureResult()
	{
		// Arrange
		var errorMessage = "Test error";

		// Act
		var result = MethodResult.Failure(errorMessage);

		// Assert
		Assert.False(result.IsSuccess);
		Assert.True(result.IsFailure);
		Assert.NotNull(result.Error);
		Assert.Equal(errorMessage, result.Error.Message);
	}

	[Fact]
	public void MethodResult_Failure_WithAsyncEndpointError_ReturnsFailureResult()
	{
		// Arrange
		var error = AsyncEndpointError.FromMessage("Test error");

		// Act
		var result = MethodResult.Failure(error);

		// Assert
		Assert.False(result.IsSuccess);
		Assert.True(result.IsFailure);
		Assert.Equal(error, result.Error);
	}

	[Fact]
	public void MethodResult_Failure_WithException_ReturnsFailureResult()
	{
		// Arrange
		var exception = new InvalidOperationException("Test exception");

		// Act
		var result = MethodResult.Failure(exception);

		// Assert
		Assert.False(result.IsSuccess);
		Assert.True(result.IsFailure);
		Assert.NotNull(result.Error);
		Assert.Equal(exception.Message, result.Error.Message);
	}

	[Fact]
	public void MethodResultT_Success_ReturnsSuccessResultWithData()
	{
		// Arrange
		var testData = "test data";

		// Act
		var result = MethodResult<string>.Success(testData);

		// Assert
		Assert.True(result.IsSuccess);
		Assert.False(result.IsFailure);
		Assert.Equal(testData, result.Data);
		Assert.Equal(testData, result.DataOrNull);
	}

	[Fact]
	public void MethodResultT_Failure_WithErrorMessage_ReturnsFailureResult()
	{
		// Arrange
		var errorMessage = "Test error";

		// Act
		var result = MethodResult<string>.Failure(errorMessage);

		// Assert
		Assert.False(result.IsSuccess);
		Assert.True(result.IsFailure);
		Assert.NotNull(result.Error);
		Assert.Equal(errorMessage, result.Error.Message);
		
		// Data property should throw when IsSuccess is false
		Assert.Throws<InvalidOperationException>(() => result.Data);
		Assert.Null(result.DataOrNull);
	}

	[Fact]
	public void MethodResultT_Failure_WithAsyncEndpointError_ReturnsFailureResult()
	{
		// Arrange
		var error = AsyncEndpointError.FromMessage("Test error");

		// Act
		var result = MethodResult<string>.Failure(error);

		// Assert
		Assert.False(result.IsSuccess);
		Assert.True(result.IsFailure);
		Assert.Equal(error, result.Error);
		
		// Data property should throw when IsSuccess is false
		Assert.Throws<InvalidOperationException>(() => result.Data);
		Assert.Null(result.DataOrNull);
	}

	[Fact]
	public void MethodResultT_Failure_WithException_ReturnsFailureResult()
	{
		// Arrange
		var exception = new InvalidOperationException("Test exception");

		// Act
		var result = MethodResult<string>.Failure(exception);

		// Assert
		Assert.False(result.IsSuccess);
		Assert.True(result.IsFailure);
		Assert.NotNull(result.Error);
		Assert.Equal(exception.Message, result.Error.Message);
		
		// Data property should throw when IsSuccess is false
		Assert.Throws<InvalidOperationException>(() => result.Data);
		Assert.Null(result.DataOrNull);
	}

	[Fact]
	public void MethodResultT_Success_WithNullData_ReturnsSuccessResultWithNull()
	{
		// Act
		var result = MethodResult<string>.Success(null);

		// Assert
		Assert.True(result.IsSuccess);
		Assert.False(result.IsFailure);
		Assert.Null(result.DataOrNull);
		// Accessing Data with null value should throw InvalidOperationException based on the implementation
		Assert.Throws<InvalidOperationException>(() => result.Data);
	}
}