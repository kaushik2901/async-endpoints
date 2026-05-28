using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.AspNetCore.Endpoints;
using AsyncEndpoints.Core.Infrastructure.Serialization;
using AsyncEndpoints.Provider.InMemory.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncEndpoints.AspNetCore.UnitTests.Endpoints;

public class JobResultEndpointTests
{
	private static (IServiceProvider Services, IJobStore Store) CreateTestServices()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(TimeProvider.System);
		services.AddSingleton<ISerializer, Serializer>();
		services.AddAsyncEndpointsInMemory();
		var provider = services.BuildServiceProvider();
		var store = provider.GetRequiredService<IJobStore>();
		return (provider, store);
	}

	[Fact]
	public async Task GetJobResult_CompletedJob_Returns200WithResult()
	{
		var (sp, store) = CreateTestServices();
		var descriptor = new JobDescriptor("TestJob", """{"data":"test"}""");
		var jobId = await store.EnqueueAsync(descriptor);
		await store.UpdateStatusAsync(jobId, JobStatus.Processing);
		await store.UpdateStatusAsync(jobId, JobStatus.Completed, "\"test result\"");

		var result = await JobResultEndpoint.GetJobResult(jobId, store, CancellationToken.None);

		var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
		Assert.Equal(StatusCodes.Status200OK, statusCodeResult.StatusCode);
	}

	[Fact]
	public async Task GetJobResult_NonExistentJob_Returns404()
	{
		var (sp, store) = CreateTestServices();

		var result = await JobResultEndpoint.GetJobResult(Guid.NewGuid(), store, CancellationToken.None);

		var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
		Assert.Equal(StatusCodes.Status404NotFound, statusCodeResult.StatusCode);
	}

	[Fact]
	public async Task GetJobResult_QueuedJob_Returns409()
	{
		var (sp, store) = CreateTestServices();
		var descriptor = new JobDescriptor("TestJob", "{}");
		var jobId = await store.EnqueueAsync(descriptor);

		var result = await JobResultEndpoint.GetJobResult(jobId, store, CancellationToken.None);

		var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
		Assert.Equal(StatusCodes.Status409Conflict, statusCodeResult.StatusCode);
	}

	[Fact]
	public async Task GetJobResult_FailedJob_Returns409()
	{
		var (sp, store) = CreateTestServices();
		var descriptor = new JobDescriptor("TestJob", "{}");
		var jobId = await store.EnqueueAsync(descriptor);
		await store.UpdateStatusAsync(jobId, JobStatus.Processing);
		await store.UpdateStatusAsync(jobId, JobStatus.Failed, "error occurred");

		var result = await JobResultEndpoint.GetJobResult(jobId, store, CancellationToken.None);

		var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
		Assert.Equal(StatusCodes.Status409Conflict, statusCodeResult.StatusCode);
	}
}
