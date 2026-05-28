using AsyncEndpoints.Abstractions.Common;

namespace AsyncEndpoints.Abstractions.UnitTests.Utilities;

public class MethodResultTests
{
	[Fact]
	public void MethodResult_Success_ReturnsSuccessResult()
	{
		var result = MethodResult.Success();

		Assert.True(result.IsSuccess);
		Assert.False(result.IsFailure);
		Assert.NotNull(result.Error);
	}

	[Fact]
	public void MethodResult_Failure_WithErrorMessage_ReturnsFailureResult()
	{
		var errorMessage = "Test error";

		var result = MethodResult.Failure(errorMessage);

		Assert.False(result.IsSuccess);
		Assert.True(result.IsFailure);
		Assert.NotNull(result.Error);
		Assert.Equal(errorMessage, result.Error.Message);
	}

	[Fact]
	public void MethodResult_Failure_WithAsyncEndpointError_ReturnsFailureResult()
	{
		var error = AsyncEndpointError.FromMessage("Test error");

		var result = MethodResult.Failure(error);

		Assert.False(result.IsSuccess);
		Assert.True(result.IsFailure);
		Assert.Equal(error, result.Error);
	}

	[Fact]
	public void MethodResult_Failure_WithException_ReturnsFailureResult()
	{
		var exception = new InvalidOperationException("Test exception");

		var result = MethodResult.Failure(exception);

		Assert.False(result.IsSuccess);
		Assert.True(result.IsFailure);
		Assert.NotNull(result.Error);
		Assert.Equal(exception.Message, result.Error.Message);
	}

	[Fact]
	public void MethodResultT_Success_ReturnsSuccessResultWithData()
	{
		var testData = "test data";

		var result = MethodResult<string>.Success(testData);

		Assert.True(result.IsSuccess);
		Assert.False(result.IsFailure);
		Assert.Equal(testData, result.Data);
		Assert.Equal(testData, result.DataOrNull);
	}

	[Fact]
	public void MethodResultT_Failure_WithErrorMessage_ReturnsFailureResult()
	{
		var errorMessage = "Test error";

		var result = MethodResult<string>.Failure(errorMessage);

		Assert.False(result.IsSuccess);
		Assert.True(result.IsFailure);
		Assert.NotNull(result.Error);
		Assert.Equal(errorMessage, result.Error.Message);

		Assert.Throws<InvalidOperationException>(() => result.Data);
		Assert.Null(result.DataOrNull);
	}

	[Fact]
	public void MethodResultT_Failure_WithAsyncEndpointError_ReturnsFailureResult()
	{
		var error = AsyncEndpointError.FromMessage("Test error");

		var result = MethodResult<string>.Failure(error);

		Assert.False(result.IsSuccess);
		Assert.True(result.IsFailure);
		Assert.Equal(error, result.Error);

		Assert.Throws<InvalidOperationException>(() => result.Data);
		Assert.Null(result.DataOrNull);
	}

	[Fact]
	public void MethodResultT_Failure_WithException_ReturnsFailureResult()
	{
		var exception = new InvalidOperationException("Test exception");

		var result = MethodResult<string>.Failure(exception);

		Assert.False(result.IsSuccess);
		Assert.True(result.IsFailure);
		Assert.NotNull(result.Error);
		Assert.Equal(exception.Message, result.Error.Message);

		Assert.Throws<InvalidOperationException>(() => result.Data);
		Assert.Null(result.DataOrNull);
	}

	[Fact]
	public void MethodResultT_Success_WithNullData_ReturnsSuccessResultWithNull()
	{
		var result = MethodResult<string>.Success(null);

		Assert.True(result.IsSuccess);
		Assert.False(result.IsFailure);
		Assert.Null(result.DataOrNull);
		Assert.Throws<InvalidOperationException>(() => result.Data);
	}
}
