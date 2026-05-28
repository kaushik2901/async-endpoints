using AsyncEndpoints.Core.Legacy.JobProcessing;
using AsyncEndpoints.Core.Legacy.Utilities;
using AsyncEndpoints.UnitTests.TestSupport;
using Moq;

namespace AsyncEndpoints.UnitTests.Utilities;

public class AsyncContextBuilderTests
{
	[Theory, AutoMoqData]
	public void Build_CreatesAsyncContextWithCorrectProperties(
		TestRequest request,
		Job job)
	{
		// Act
		var context = AsyncContextBuilder.Build(request, job);

		// Assert
		Assert.Equal(request, context.Request);
		Assert.Equal(job.Headers, context.Headers);
		Assert.Equal(job.RouteParams, context.RouteParams);
		Assert.Equal(job.QueryParams, context.QueryParams);
	}

	[Theory, AutoMoqData]
	public void Build_WithEmptyCollections_HandlesCorrectly(
		TestRequest request)
	{
		// Arrange
		var mockDateTimeProvider = new Mock<Abstractions.Infrastructure.IDateTimeProvider>();
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(DateTimeOffset.UtcNow);
		var job = Job.Create(
			Guid.NewGuid(),
			"",
			"{}",
			[],
			[],
			[],
			3,
			mockDateTimeProvider.Object);

		// Act
		var context = AsyncContextBuilder.Build(request, job);

		// Assert
		Assert.Equal(request, context.Request);
		Assert.Equal(job.Headers, context.Headers);
		Assert.Equal(job.RouteParams, context.RouteParams);
		Assert.Equal(job.QueryParams, context.QueryParams);
	}
}
