using AsyncEndpoints.Abstractions.Submission;
using AsyncEndpoints.AspNetCore.Endpoints;
using AsyncEndpoints.Core.Infrastructure.Serialization;
using AsyncEndpoints.Core.Submission;
using AsyncEndpoints.Provider.InMemory.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace AsyncEndpoints.AspNetCore.UnitTests.Endpoints;

public class JobEndpointsTests
{
	private static (IServiceProvider Services, IJobSubmitter Submitter) CreateTestServices()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(TimeProvider.System);
		services.AddSingleton<ISerializer, Serializer>();
		services.AddAsyncEndpointsInMemory();
		services.AddSingleton<IJobSubmitter, JobSubmitter>();
		var provider = services.BuildServiceProvider();
		var submitter = provider.GetRequiredService<IJobSubmitter>();
		return (provider, submitter);
	}

	[Fact]
	public async Task PostJob_ValidBody_Returns202AcceptedWithJobId()
	{
		var (sp, submitter) = CreateTestServices();
		var httpContext = new DefaultHttpContext();
		httpContext.RequestServices = sp;
		var body = """{"data":"test_value"}""";
		httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
		httpContext.Request.ContentType = "application/json";

		var result = await JobEndpoints.PostJob(httpContext, submitter, CancellationToken.None);

		var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
		Assert.Equal(StatusCodes.Status202Accepted, statusCodeResult.StatusCode);
	}

	[Fact]
	public async Task PostJob_InvalidBody_Returns400()
	{
		var (sp, submitter) = CreateTestServices();
		var httpContext = new DefaultHttpContext();
		httpContext.RequestServices = sp;
		httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("not valid json"));
		httpContext.Request.ContentType = "application/json";

		var result = await JobEndpoints.PostJob(httpContext, submitter, CancellationToken.None);

		var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
		Assert.Equal(StatusCodes.Status400BadRequest, statusCodeResult.StatusCode);
	}

	[Fact]
	public async Task PostJob_EmptyBody_Returns400()
	{
		var (sp, submitter) = CreateTestServices();
		var httpContext = new DefaultHttpContext();
		httpContext.RequestServices = sp;
		httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(""));
		httpContext.Request.ContentType = "application/json";

		var result = await JobEndpoints.PostJob(httpContext, submitter, CancellationToken.None);

		var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
		Assert.Equal(StatusCodes.Status400BadRequest, statusCodeResult.StatusCode);
	}

	[Fact]
	public async Task PostJob_UsesSpecifiedChannel()
	{
		var (sp, submitter) = CreateTestServices();
		var httpContext = new DefaultHttpContext();
		httpContext.RequestServices = sp;
		httpContext.Request.QueryString = new QueryString("?channel=custom-channel");
		var body = """{"data":"test"}""";
		httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
		httpContext.Request.ContentType = "application/json";

		var result = await JobEndpoints.PostJob(httpContext, submitter, CancellationToken.None);

		var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
		Assert.Equal(StatusCodes.Status202Accepted, statusCodeResult.StatusCode);
	}

	[Fact]
	public async Task PostJob_UsesXChannelHeader()
	{
		var (sp, submitter) = CreateTestServices();
		var httpContext = new DefaultHttpContext();
		httpContext.RequestServices = sp;
		httpContext.Request.Headers["X-Channel"] = "header-channel";
		var body = """{"data":"test"}""";
		httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
		httpContext.Request.ContentType = "application/json";

		var result = await JobEndpoints.PostJob(httpContext, submitter, CancellationToken.None);

		var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
		Assert.Equal(StatusCodes.Status202Accepted, statusCodeResult.StatusCode);
	}
}
