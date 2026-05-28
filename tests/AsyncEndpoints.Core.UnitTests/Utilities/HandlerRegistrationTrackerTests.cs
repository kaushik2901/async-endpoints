using AsyncEndpoints.Abstractions.Infrastructure;
using AsyncEndpoints.Abstractions.Utilities;
using AsyncEndpoints.Core.Legacy.JobProcessing;
using AsyncEndpoints.Core.Legacy.Utilities;
using AsyncEndpoints.Core.UnitTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AsyncEndpoints.Core.UnitTests.Utilities;

public class HandlerRegistrationTrackerTests
{
	[Fact]
	public void Register_AddsHandlerToRegistry()
	{
		var jobName = nameof(Register_AddsHandlerToRegistry);
		Func<IServiceProvider, TestRequest, Job, CancellationToken, Task<MethodResult<TestResponse>>> handlerFunc =
			(provider, request, job, token) => Task.FromResult(MethodResult<TestResponse>.Success(new TestResponse { Value = "result" }));

		HandlerRegistrationTracker.Register(jobName, handlerFunc);

		var registration = HandlerRegistrationTracker.GetHandlerRegistration(jobName);
		Assert.NotNull(registration);
		Assert.Equal(jobName, registration.JobName);
		Assert.Equal(typeof(TestRequest), registration.RequestType);
		Assert.Equal(typeof(TestResponse), registration.ResponseType);
	}

	[Fact]
	public void GetHandlerRegistration_ReturnsNullForNonExistentJob()
	{
		var registration = HandlerRegistrationTracker.GetHandlerRegistration("non-existent-job");

		Assert.Null(registration);
	}

	[Fact]
	public async Task GetInvoker_ReturnsInvokerForRegisteredJob()
	{
		var jobName = nameof(GetInvoker_ReturnsInvokerForRegisteredJob);
		var serviceProvider = new ServiceCollection().BuildServiceProvider();
		var mockDateTimeProvider = new Mock<IDateTimeProvider>();
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(DateTimeOffset.UtcNow);
		var testJob = Job.Create(
			Guid.NewGuid(),
			"TestJob",
			"{}",
			[],
			[],
			[],
			3,
			mockDateTimeProvider.Object);
		var testRequest = new TestRequest { Value = "request" };

		Func<IServiceProvider, TestRequest, Job, CancellationToken, Task<MethodResult<TestResponse>>> handlerFunc =
			(provider, request, job, token) => Task.FromResult(MethodResult<TestResponse>.Success(new TestResponse { Value = "result" }));

		HandlerRegistrationTracker.Register(jobName, handlerFunc);
		var invoker = HandlerRegistrationTracker.GetInvoker(jobName);

		Assert.NotNull(invoker);

		var result = await invoker(serviceProvider, testRequest, testJob, CancellationToken.None);
		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Data);
		Assert.Equal("result", ((TestResponse)result.Data).Value);
	}

	[Fact]
	public void GetInvoker_ReturnsNullForNonExistentJob()
	{
		var invoker = HandlerRegistrationTracker.GetInvoker("non-existent-job");

		Assert.Null(invoker);
	}

	[Fact]
	public void RegisterMultipleHandlers_EachIsAccessible()
	{
		var jobName1 = $"{nameof(RegisterMultipleHandlers_EachIsAccessible)}-1";
		var jobName2 = $"{nameof(RegisterMultipleHandlers_EachIsAccessible)}-2";

		Func<IServiceProvider, TestRequest, Job, CancellationToken, Task<MethodResult<TestResponse>>> handlerFunc1 =
			(provider, request, job, token) => Task.FromResult(MethodResult<TestResponse>.Success(new TestResponse { Value = "result1" }));

		Func<IServiceProvider, TestRequest, Job, CancellationToken, Task<MethodResult<TestResponse>>> handlerFunc2 =
			(provider, request, job, token) => Task.FromResult(MethodResult<TestResponse>.Success(new TestResponse { Value = "result2" }));

		HandlerRegistrationTracker.Register(jobName1, handlerFunc1);
		HandlerRegistrationTracker.Register(jobName2, handlerFunc2);

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
