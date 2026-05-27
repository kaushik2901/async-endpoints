using AsyncEndpoints.Abstractions.Submission;

namespace AsyncEndpoints.Abstractions.UnitTests.Submission;

public class IJobSubmitterTests
{
    [Fact]
    public void Interface_IsPublic()
    {
        Assert.True(typeof(IJobSubmitter).IsPublic);
    }

    [Fact]
    public void SubmitAsync_ReturnsGuid()
    {
        var method = typeof(IJobSubmitter).GetMethod("SubmitAsync");
        Assert.NotNull(method);
        var returnType = method!.ReturnType;
        Assert.True(returnType == typeof(Task<Guid>) || returnType.GetGenericTypeDefinition() == typeof(Task<>));
    }
}
