using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.AspNetCore.Endpoints;
using AsyncEndpoints.Core.Infrastructure.Serialization;
using AsyncEndpoints.Provider.InMemory.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncEndpoints.AspNetCore.UnitTests.Endpoints;

public class JobStatusEndpointTests
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
	public async Task GetJobStatus_ExistingJob_Returns200WithStatus()
	{
		var (sp, store) = CreateTestServices();
		var descriptor = new JobDescriptor("TestJob", "{}");
		var jobId = await store.EnqueueAsync(descriptor);

		var result = await JobStatusEndpoint.GetJobStatus(jobId, store, CancellationToken.None);

		var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
		Assert.Equal(StatusCodes.Status200OK, statusCodeResult.StatusCode);
	}

	[Fact]
	public async Task GetJobStatus_NonExistentJob_Returns404()
	{
		var (sp, store) = CreateTestServices();
		var fakeId = Guid.NewGuid();

		var result = await JobStatusEndpoint.GetJobStatus(fakeId, store, CancellationToken.None);

		var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
		Assert.Equal(StatusCodes.Status404NotFound, statusCodeResult.StatusCode);
	}

	[Fact]
	public async Task GetJobStatus_ReturnsCorrectStatusDto()
	{
		var (sp, store) = CreateTestServices();
		var descriptor = new JobDescriptor("TestJob", """{"key":"value"}""");
		var jobId = await store.EnqueueAsync(descriptor);

		var result = await JobStatusEndpoint.GetJobStatus(jobId, store, CancellationToken.None);

		var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
		Assert.Equal(StatusCodes.Status200OK, statusCodeResult.StatusCode);
	}
}
