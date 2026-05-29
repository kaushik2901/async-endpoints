using AsyncEndpoints.Abstractions.Infrastructure.Serialization;
using AsyncEndpoints.Abstractions.Submission;
using AsyncEndpoints.AspNetCore.Configuration;
using AsyncEndpoints.AspNetCore.Handlers;
using AsyncEndpoints.AspNetCore.UnitTests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Text;

namespace AsyncEndpoints.AspNetCore.UnitTests.Handlers;

public class JobSubmitHandlerTests
{
	private static DefaultHttpContext CreateHttpContext()
	{
		var httpContext = new DefaultHttpContext();
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddHttpContextAccessor();
		services.AddOptions();
		services.AddProblemDetails();
		httpContext.RequestServices = services.BuildServiceProvider();
		return httpContext;
	}

	[Fact]
	public async Task HandleWithBody_NoHandler_SubmitsJobAndReturns202()
	{
		var httpContext = CreateHttpContext();
		httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("""{"Value":"test"}"""));

		var jobId = Guid.NewGuid();
		var submitter = new Mock<IJobSubmitter>();
		submitter.Setup(s => s.SubmitAsync("TestJob", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(jobId);

		var serializer = new Mock<ISerializer>();
		serializer.Setup(s => s.Deserialize<TestRequest>(It.IsAny<string>())).Returns(new TestRequest { Value = "test" });

		var responseConfig = new AsyncEndpointsResponseConfigurations();

		await JobSubmitHandler.HandleJobSubmission<TestRequest>(
			httpContext, "TestJob", null,
			submitter.Object, responseConfig, serializer.Object, CancellationToken.None);

		Assert.Equal(StatusCodes.Status202Accepted, httpContext.Response.StatusCode);
		submitter.Verify(s => s.SubmitAsync("TestJob", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task HandleWithBody_HandlerReturnsResult_ExecutesResultAndDoesNotSubmit()
	{
		var httpContext = CreateHttpContext();
		httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("""{"Value":"test"}"""));

		var submitter = new Mock<IJobSubmitter>();
		var serializer = new Mock<ISerializer>();
		serializer.Setup(s => s.Deserialize<TestRequest>(It.IsAny<string>())).Returns(new TestRequest());
		var responseConfig = new AsyncEndpointsResponseConfigurations();

		Task<IResult?> Handler(HttpContext ctx, TestRequest req, CancellationToken ct)
			=> Task.FromResult<IResult?>(Results.Ok());

		await JobSubmitHandler.HandleJobSubmission<TestRequest>(
			httpContext, "TestJob", Handler,
			submitter.Object, responseConfig, serializer.Object, CancellationToken.None);

		Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
		submitter.Verify(s => s.SubmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task HandleWithBody_HandlerReturnsNullTask_FallsBackToSubmission()
	{
		var httpContext = CreateHttpContext();
		httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("""{"Value":"test"}"""));

		var jobId = Guid.NewGuid();
		var submitter = new Mock<IJobSubmitter>();
		submitter.Setup(s => s.SubmitAsync("TestJob", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(jobId);

		var serializer = new Mock<ISerializer>();
		serializer.Setup(s => s.Deserialize<TestRequest>(It.IsAny<string>())).Returns(new TestRequest());
		var responseConfig = new AsyncEndpointsResponseConfigurations();

		Task<IResult?>? Handler(HttpContext ctx, TestRequest req, CancellationToken ct) => null;

		await JobSubmitHandler.HandleJobSubmission<TestRequest>(
			httpContext, "TestJob", Handler,
			submitter.Object, responseConfig, serializer.Object, CancellationToken.None);

		Assert.Equal(StatusCodes.Status202Accepted, httpContext.Response.StatusCode);
		submitter.Verify(s => s.SubmitAsync("TestJob", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task HandleWithBody_HandlerReturnsNullResult_FallsBackToSubmission()
	{
		var httpContext = CreateHttpContext();
		httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("""{"Value":"test"}"""));

		var jobId = Guid.NewGuid();
		var submitter = new Mock<IJobSubmitter>();
		submitter.Setup(s => s.SubmitAsync("TestJob", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(jobId);

		var serializer = new Mock<ISerializer>();
		serializer.Setup(s => s.Deserialize<TestRequest>(It.IsAny<string>())).Returns(new TestRequest());
		var responseConfig = new AsyncEndpointsResponseConfigurations();

		Task<IResult?>? Handler(HttpContext ctx, TestRequest req, CancellationToken ct)
			=> Task.FromResult<IResult?>(null);

		await JobSubmitHandler.HandleJobSubmission<TestRequest>(
			httpContext, "TestJob", Handler,
			submitter.Object, responseConfig, serializer.Object, CancellationToken.None);

		Assert.Equal(StatusCodes.Status202Accepted, httpContext.Response.StatusCode);
		submitter.Verify(s => s.SubmitAsync("TestJob", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task HandleWithBody_BodyReadFails_Returns400()
	{
		var httpContext = CreateHttpContext();
		var throwStream = new MemoryStream();
		throwStream.Dispose();
		httpContext.Request.Body = throwStream;

		var submitter = new Mock<IJobSubmitter>();
		var serializer = new Mock<ISerializer>();
		var responseConfig = new AsyncEndpointsResponseConfigurations();

		await JobSubmitHandler.HandleJobSubmission<TestRequest>(
			httpContext, "TestJob", null,
			submitter.Object, responseConfig, serializer.Object, CancellationToken.None);

		Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
		submitter.Verify(s => s.SubmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task HandleWithBody_DeserializationFails_Returns400()
	{
		var httpContext = CreateHttpContext();
		httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("""{"Value":"test"}"""));

		var submitter = new Mock<IJobSubmitter>();
		var serializer = new Mock<ISerializer>();
		serializer.Setup(s => s.Deserialize<TestRequest>(It.IsAny<string>())).Throws(new InvalidOperationException("bad json"));
		var responseConfig = new AsyncEndpointsResponseConfigurations();

		await JobSubmitHandler.HandleJobSubmission<TestRequest>(
			httpContext, "TestJob", null,
			submitter.Object, responseConfig, serializer.Object, CancellationToken.None);

		Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
		submitter.Verify(s => s.SubmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task HandleWithBody_SubmissionFails_Returns500()
	{
		var httpContext = CreateHttpContext();
		httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("""{"Value":"test"}"""));

		var submitter = new Mock<IJobSubmitter>();
		submitter.Setup(s => s.SubmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("submission failed"));

		var serializer = new Mock<ISerializer>();
		serializer.Setup(s => s.Deserialize<TestRequest>(It.IsAny<string>())).Returns(new TestRequest());
		var responseConfig = new AsyncEndpointsResponseConfigurations();

		await JobSubmitHandler.HandleJobSubmission<TestRequest>(
			httpContext, "TestJob", null,
			submitter.Object, responseConfig, serializer.Object, CancellationToken.None);

		Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
	}

	[Fact]
	public async Task HandleWithoutBody_HandlerReturnsResult_ExecutesResultAndDoesNotSubmit()
	{
		var httpContext = CreateHttpContext();
		var submitter = new Mock<IJobSubmitter>();
		var responseConfig = new AsyncEndpointsResponseConfigurations();

		Task<IResult?>? Handler(HttpContext ctx, CancellationToken ct)
			=> Task.FromResult<IResult?>(Results.Ok());

		await JobSubmitHandler.HandleJobSubmission(
			httpContext, "TestJob", Handler,
			submitter.Object, responseConfig, CancellationToken.None);

		Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
		submitter.Verify(s => s.SubmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task HandleWithoutBody_NoHandler_SubmitsJobAndReturns202()
	{
		var httpContext = CreateHttpContext();
		httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

		var jobId = Guid.NewGuid();
		var submitter = new Mock<IJobSubmitter>();
		submitter.Setup(s => s.SubmitAsync("TestJob", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(jobId);

		var responseConfig = new AsyncEndpointsResponseConfigurations();

		await JobSubmitHandler.HandleJobSubmission(
			httpContext, "TestJob", (Func<HttpContext, CancellationToken, Task<IResult?>?>?)null,
			submitter.Object, responseConfig, CancellationToken.None);

		Assert.Equal(StatusCodes.Status202Accepted, httpContext.Response.StatusCode);
		submitter.Verify(s => s.SubmitAsync("TestJob", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task HandleWithoutBody_HandlerReturnsNullTask_FallsBackToSubmission()
	{
		var httpContext = CreateHttpContext();
		httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

		var jobId = Guid.NewGuid();
		var submitter = new Mock<IJobSubmitter>();
		submitter.Setup(s => s.SubmitAsync("TestJob", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(jobId);

		var responseConfig = new AsyncEndpointsResponseConfigurations();

		Task<IResult?>? Handler(HttpContext ctx, CancellationToken ct) => null;

		await JobSubmitHandler.HandleJobSubmission(
			httpContext, "TestJob", Handler,
			submitter.Object, responseConfig, CancellationToken.None);

		Assert.Equal(StatusCodes.Status202Accepted, httpContext.Response.StatusCode);
		submitter.Verify(s => s.SubmitAsync("TestJob", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task HandleWithoutBody_HandlerReturnsNullResult_FallsBackToSubmission()
	{
		var httpContext = CreateHttpContext();
		httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

		var jobId = Guid.NewGuid();
		var submitter = new Mock<IJobSubmitter>();
		submitter.Setup(s => s.SubmitAsync("TestJob", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(jobId);

		var responseConfig = new AsyncEndpointsResponseConfigurations();

		Task<IResult?>? Handler(HttpContext ctx, CancellationToken ct)
			=> Task.FromResult<IResult?>(null);

		await JobSubmitHandler.HandleJobSubmission(
			httpContext, "TestJob", Handler,
			submitter.Object, responseConfig, CancellationToken.None);

		Assert.Equal(StatusCodes.Status202Accepted, httpContext.Response.StatusCode);
		submitter.Verify(s => s.SubmitAsync("TestJob", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task HandleWithoutBody_SubmissionFails_Returns500()
	{
		var httpContext = CreateHttpContext();
		httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

		var submitter = new Mock<IJobSubmitter>();
		submitter.Setup(s => s.SubmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("submission failed"));

		var responseConfig = new AsyncEndpointsResponseConfigurations();

		await JobSubmitHandler.HandleJobSubmission(
			httpContext, "TestJob", (Func<HttpContext, CancellationToken, Task<IResult?>?>?)null,
			submitter.Object, responseConfig, CancellationToken.None);

		Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
	}
}
