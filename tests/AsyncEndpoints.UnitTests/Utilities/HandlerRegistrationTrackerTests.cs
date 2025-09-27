using AsyncEndpoints.Entities;
using AsyncEndpoints.UnitTests.TestSupport;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncEndpoints.UnitTests.Utilities;

public class HandlerRegistrationTrackerTests
{
    [Fact]
    public void Register_AddsHandlerToRegistry()
    {
        // Arrange
        var jobName = "test-job";
        Func<IServiceProvider, TestRequest, Job, CancellationToken, Task<MethodResult<TestResponse>>> handlerFunc =
            (provider, request, job, token) => Task.FromResult(MethodResult<TestResponse>.Success(new TestResponse { Value = "result" }));

        // Act
        HandlerRegistrationTracker.Register(jobName, handlerFunc);

        // Assert
        var registration = HandlerRegistrationTracker.GetHandlerRegistration(jobName);
        Assert.NotNull(registration);
        Assert.Equal(jobName, registration.JobName);
        Assert.Equal(typeof(TestRequest), registration.RequestType);
        Assert.Equal(typeof(TestResponse), registration.ResponseType);
    }

    [Fact]
    public void GetHandlerRegistration_ReturnsNullForNonExistentJob()
    {
        // Act
        var registration = HandlerRegistrationTracker.GetHandlerRegistration("non-existent-job");

        // Assert
        Assert.Null(registration);
    }

    [Fact]
    public async Task GetInvoker_ReturnsInvokerForRegisteredJob()
    {
        // Arrange
        var jobName = "test-job";
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var testJob = new Job();
        var testRequest = new TestRequest { Value = "request" };

        Func<IServiceProvider, TestRequest, Job, CancellationToken, Task<MethodResult<TestResponse>>> handlerFunc =
            (provider, request, job, token) => Task.FromResult(MethodResult<TestResponse>.Success(new TestResponse { Value = "result" }));

        // Act
        HandlerRegistrationTracker.Register(jobName, handlerFunc);
        var invoker = HandlerRegistrationTracker.GetInvoker(jobName);

        // Assert
        Assert.NotNull(invoker);

        var result = await invoker(serviceProvider, testRequest, testJob, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("result", ((TestResponse)result.Data!).Value);
    }

    [Fact]
    public void GetInvoker_ReturnsNullForNonExistentJob()
    {
        // Act
        var invoker = HandlerRegistrationTracker.GetInvoker("non-existent-job");

        // Assert
        Assert.Null(invoker);
    }

    [Fact]
    public void RegisterMultipleHandlers_EachIsAccessible()
    {
        // Arrange
        var jobName1 = "job1";
        var jobName2 = "job2";

        Func<IServiceProvider, TestRequest, Job, CancellationToken, Task<MethodResult<TestResponse>>> handlerFunc1 =
            (provider, request, job, token) => Task.FromResult(MethodResult<TestResponse>.Success(new TestResponse { Value = "result1" }));

        Func<IServiceProvider, TestRequest, Job, CancellationToken, Task<MethodResult<TestResponse>>> handlerFunc2 =
            (provider, request, job, token) => Task.FromResult(MethodResult<TestResponse>.Success(new TestResponse { Value = "result2" }));

        // Act
        HandlerRegistrationTracker.Register(jobName1, handlerFunc1);
        HandlerRegistrationTracker.Register(jobName2, handlerFunc2);

        // Assert
        var registration1 = HandlerRegistrationTracker.GetHandlerRegistration(jobName1);
        var registration2 = HandlerRegistrationTracker.GetHandlerRegistration(jobName2);

        Assert.NotNull(registration1);
        Assert.NotNull(registration2);
        Assert.Equal(jobName1, registration1.JobName);
        Assert.Equal(jobName2, registration2.JobName);

        var invoker1 = HandlerRegistrationTracker.GetInvoker(jobName1);
        var invoker2 = HandlerRegistrationTracker.GetInvoker(jobName2);

        Assert.NotNull(invoker1);
        Assert.NotNull(invoker2);
    }
}