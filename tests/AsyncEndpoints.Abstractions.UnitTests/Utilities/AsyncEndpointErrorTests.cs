using AsyncEndpoints.Abstractions.Utilities;
using AsyncEndpoints.Abstractions.UnitTests.TestSupport;

namespace AsyncEndpoints.Abstractions.UnitTests.Utilities;

public class AsyncEndpointErrorTests
{
	[Theory, AutoMoqData]
	public void Constructor_SetsPropertiesCorrectly(
		string code,
		string message,
		InvalidOperationException exception)
	{
		var error = new AsyncEndpointError(code, message, exception);

		Assert.Equal(code, error.Code);
		Assert.Equal(message, error.Message);
		Assert.NotNull(error.Exception);
		Assert.Equal(exception.GetType().Name, error.Exception.Type);
		Assert.Equal(exception.Message, error.Exception.Message);
	}

	[Theory, AutoMoqData]
	public void Constructor_WithNullCode_ThrowsArgumentNullException(
		string message,
		InvalidOperationException exception)
	{
		Assert.Throws<ArgumentNullException>(() => new AsyncEndpointError(null!, message, exception));
	}

	[Theory, AutoMoqData]
	public void Constructor_WithNullMessage_ThrowsArgumentNullException(
		string code,
		InvalidOperationException exception)
	{
		Assert.Throws<ArgumentNullException>(() => new AsyncEndpointError(code, null!, exception));
	}

	[Theory, AutoMoqData]
	public void FromMessage_CreatesErrorWithUnknownCode(
		string message,
		InvalidOperationException exception)
	{
		var error = AsyncEndpointError.FromMessage(message, exception);

		Assert.Equal("UNKNOWN", error.Code);
		Assert.Equal(message, error.Message);
		Assert.NotNull(error.Exception);
		Assert.Equal(exception.GetType().Name, error.Exception.Type);
		Assert.Equal(exception.Message, error.Exception.Message);
	}

	[Theory, AutoMoqData]
	public void FromMessage_WithoutException_CreatesError(
		string message)
	{
		var error = AsyncEndpointError.FromMessage(message);

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
		var error = AsyncEndpointError.FromCode(code, message, exception);

		Assert.Equal(code, error.Code);
		Assert.Equal(message, error.Message);
		Assert.NotNull(error.Exception);
		Assert.Equal(exception.GetType().Name, error.Exception.Type);
		Assert.Equal(exception.Message, error.Exception.Message);
	}

	[Theory, AutoMoqData]
	public void FromCode_WithoutException_CreatesError(
		string code,
		string message)
	{
		var error = AsyncEndpointError.FromCode(code, message);

		Assert.Equal(code, error.Code);
		Assert.Equal(message, error.Message);
		Assert.Null(error.Exception);
	}

	[Theory, AutoMoqData]
	public void FromException_CreatesErrorFromException(
		InvalidOperationException exception)
	{
		var error = AsyncEndpointError.FromException(exception);

		Assert.Equal("INVALIDOPERATIONEXCEPTION", error.Code);
		Assert.Equal(exception.Message, error.Message);
		Assert.NotNull(error.Exception);
		Assert.Equal(exception.GetType().Name, error.Exception.Type);
		Assert.Equal(exception.Message, error.Exception.Message);
	}

	[Fact]
	public void FromException_WithNullException_ThrowsArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() => AsyncEndpointError.FromException(null!));
	}
}
