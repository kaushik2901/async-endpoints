using AsyncEndpoints.Core.Legacy.JobProcessing;
using AsyncEndpoints.Core.Legacy.Utilities;
using AsyncEndpoints.Core.UnitTests.TestSupport;
using Moq;

namespace AsyncEndpoints.Core.UnitTests.Utilities;

public class AsyncContextBuilderTests
{
	[Theory, AutoMoqData]
	public void Build_CreatesAsyncContextWithCorrectProperties(
		TestRequest request,
		Job job)
	{
		var context = AsyncContextBuilder.Build(request, job);

		Assert.Equal(request, context.Request);
		Assert.Equal(job.Headers, context.Headers);
		Assert.Equal(job.RouteParams, context.RouteParams);
		Assert.Equal(job.QueryParams, context.QueryParams);
	}

	[Theory, AutoMoqData]
	public void Build_WithEmptyCollections_HandlesCorrectly(
		TestRequest request)
	{
		var mockDateTimeProvider = new Mock<TimeProvider>();
		mockDateTimeProvider.Setup(x => x.GetUtcNow()).Returns(DateTimeOffset.UtcNow);
		var job = Job.Create(
			Guid.NewGuid(),
			"",
			"{}",
			[],
			[],
			[],
			3,
			mockDateTimeProvider.Object);

		var context = AsyncContextBuilder.Build(request, job);

		Assert.Equal(request, context.Request);
		Assert.Equal(job.Headers, context.Headers);
		Assert.Equal(job.RouteParams, context.RouteParams);
		Assert.Equal(job.QueryParams, context.QueryParams);
	}
}
