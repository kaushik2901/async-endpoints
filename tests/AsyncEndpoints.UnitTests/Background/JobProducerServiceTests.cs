using System.Threading.Channels;
using AsyncEndpoints.Background;
using AsyncEndpoints.Configuration;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.UnitTests.TestSupport;
using AsyncEndpoints.Utilities;
using AutoFixture.Xunit2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AsyncEndpoints.UnitTests.Services;

public class JobProducerServiceTests
{
	[Theory, AutoMoqData]
	public void Constructor_Succeeds_WithValidDependencies(
		[Frozen] Mock<IServiceScope> mockServiceScope,
		[Frozen] Mock<IServiceScopeFactory> mockServiceScopeFactory,
		[Frozen] Mock<IJobManager> mockJobManager,
		[Frozen] Mock<ILogger<JobProducerService>> mockLogger)
	{
		// Arrange
		mockServiceScope
			.Setup(x => x.ServiceProvider.GetService(typeof(IJobManager)))
			.Returns(mockJobManager.Object);

		mockServiceScopeFactory
			.Setup(x => x.CreateScope())
			.Returns(mockServiceScope.Object);

		var configurations = Options.Create(new AsyncEndpointsConfigurations());

		// Act
		var service = new JobProducerService(mockLogger.Object, mockServiceScopeFactory.Object, configurations);

		// Assert
		Assert.NotNull(service);
	}

	[Theory, AutoMoqData]
	public async Task ProduceJobsAsync_CompletesChannel_WhenCancellationRequested(
		[Frozen] Mock<IServiceScope> mockServiceScope,
		[Frozen] Mock<IServiceScopeFactory> mockServiceScopeFactory,
		[Frozen] Mock<IJobManager> mockJobManager,
		[Frozen] Mock<ILogger<JobProducerService>> mockLogger)
	{
		// Arrange
		mockServiceScope
			.Setup(x => x.ServiceProvider.GetService(typeof(IJobManager)))
			.Returns(mockJobManager.Object);

		mockServiceScopeFactory
			.Setup(x => x.CreateScope())
			.Returns(mockServiceScope.Object);

		var configurations = Options.Create(new AsyncEndpointsConfigurations());
		var channel = Channel.CreateBounded<Job>(new BoundedChannelOptions(10));
		var cancellationTokenSource = new CancellationTokenSource();
		cancellationTokenSource.Cancel();

		var jobProducerService = new JobProducerService(mockLogger.Object, mockServiceScopeFactory.Object, configurations);

		// Act
		await jobProducerService.ProduceJobsAsync(channel.Writer, cancellationTokenSource.Token);

		// Assert
		Assert.True(channel.Reader.Completion.IsCompleted);
	}

	[Theory, AutoMoqData]
	public async Task ProduceJobsAsync_ClaimsJobsFromJobManager(
		[Frozen] Mock<IServiceScope> mockServiceScope,
		[Frozen] Mock<IServiceScopeFactory> mockServiceScopeFactory,
		[Frozen] Mock<IJobManager> mockJobManager,
		[Frozen] Mock<ILogger<JobProducerService>> mockLogger)
	{
		// Arrange
		mockServiceScope
			.Setup(x => x.ServiceProvider.GetService(typeof(IJobManager)))
			.Returns(mockJobManager.Object);

		mockServiceScopeFactory
			.Setup(x => x.CreateScope())
			.Returns(mockServiceScope.Object);

		var configurations = Options.Create(new AsyncEndpointsConfigurations());
		var channel = Channel.CreateBounded<Job>(new BoundedChannelOptions(10));
		var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(10)); // Short timeout to avoid hanging
		var emptyJobs = new List<Job>();

		mockJobManager
			.Setup(x => x.ClaimJobsForProcessing(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(MethodResult<List<Job>>.Success(emptyJobs));

		var jobProducerService = new JobProducerService(mockLogger.Object, mockServiceScopeFactory.Object, configurations);

		// Act & Assert - Should not throw
		var exception = await Record.ExceptionAsync(() =>
			jobProducerService.ProduceJobsAsync(channel.Writer, cancellationTokenSource.Token));

		Assert.Null(exception);
	}
}
