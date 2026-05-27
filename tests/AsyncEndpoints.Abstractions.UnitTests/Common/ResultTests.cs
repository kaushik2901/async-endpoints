using AsyncEndpoints.Abstractions.Common;

namespace AsyncEndpoints.Abstractions.UnitTests.Common;

public class ResultTests
{
    [Fact]
    public void Success_SetsIsSuccessAndValue()
    {
        var result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Success_WithNullValue_SetsIsSuccess()
    {
        var result = Result<string?>.Success(null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_SetsIsFailureAndError()
    {
        var result = Result<int>.Failure("Something went wrong");

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(0, result.Value);
        Assert.Equal("Something went wrong", result.Error);
    }

    [Fact]
    public void Success_WithReferenceType_StoresValue()
    {
        var value = "hello";
        var result = Result<string>.Success(value);

        Assert.Same(value, result.Value);
    }
}
