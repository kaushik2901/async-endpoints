using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.UnitTests.TestSupport;
using AsyncEndpoints.Utilities;

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
		var job = new Job
		{
			Headers = new Dictionary<string, List<string?>>(),
			RouteParams = new Dictionary<string, object?>(),
			QueryParams = new List<KeyValuePair<string, List<string?>>>()
		};

		// Act
		var context = AsyncContextBuilder.Build(request, job);

		// Assert
		Assert.Equal(request, context.Request);
		Assert.Equal(job.Headers, context.Headers);
		Assert.Equal(job.RouteParams, context.RouteParams);
		Assert.Equal(job.QueryParams, context.QueryParams);
	}
}
