using AsyncEndpoints.Abstractions.Jobs;

namespace AsyncEndpoints.Abstractions.UnitTests.Jobs;

public class IJobHandlerTests
{
    [Fact]
    public void Interface_IsPublic()
    {
        Assert.True(typeof(IJobHandler<>).IsPublic);
    }

    [Fact]
    public void Interface_HasHandleAsyncMethod()
    {
        var method = typeof(IJobHandler<string>).GetMethod("HandleAsync");
        Assert.NotNull(method);
        Assert.True(typeof(Task).IsAssignableFrom(method!.ReturnType));
    }

    [Fact]
    public void Interface_HasCancellationTokenParameter()
    {
        var method = typeof(IJobHandler<string>).GetMethod("HandleAsync");
        var parameters = method!.GetParameters();
        Assert.Contains(parameters, p => p.ParameterType == typeof(CancellationToken));
    }
}
