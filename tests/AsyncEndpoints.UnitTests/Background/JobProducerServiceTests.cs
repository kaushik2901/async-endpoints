using AsyncEndpoints.Background;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.UnitTests.TestSupport;
using AsyncEndpoints.Worker.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Threading.Channels;

namespace AsyncEndpoints.UnitTests.Background;

public class JobProducerServiceTests
{
	[Theory, AutoMoqData]
	public void Constructor_Succeeds_WithValidDependencies(
		Mock<ILogger<JobProducerService>> mockLogger,
		Mock<IDelayCalculatorService> mockDelayCalculatorService,
		Mock<IServiceScopeFactory> mockServiceScopeFactory)
	{
		// Arrange
		var workerOptions = new WorkerOptions();

		// Act
		var service = new JobProducerService(
			mockLogger.Object,
			workerOptions,
			mockDelayCalculatorService.Object,
			mockServiceScopeFactory.Object);

		// Assert
		Assert.NotNull(service);
	}

	[Theory, AutoMoqData]
	public async Task ProduceJobsAsync_CompletesChannel_WhenCancellationRequested(
		Mock<ILogger<JobProducerService>> mockLogger,
		Mock<IDelayCalculatorService> mockDelayCalculatorService,
		Mock<IServiceScopeFactory> mockServiceScopeFactory,
		Mock<IServiceScope> mockServiceScope,
		Mock<IJobClaimingService> mockJobClaimingService)
	{
		// Arrange
		var workerOptions = new WorkerOptions();
		mockServiceScopeFactory
			.Setup(x => x.CreateScope())
			.Returns(mockServiceScope.Object);

		mockServiceScope
			.Setup(x => x.ServiceProvider.GetService(typeof(IJobClaimingService)))
			.Returns(mockJobClaimingService);

		var channel = Channel.CreateBounded<Job>(new BoundedChannelOptions(10));
		var cancellationTokenSource = new CancellationTokenSource();
		cancellationTokenSource.Cancel(); // Cancel immediately

		var jobProducerService = new JobProducerService(
			mockLogger.Object,
			workerOptions,
			mockDelayCalculatorService.Object,
			mockServiceScopeFactory.Object);

		// Act
		await jobProducerService.ProduceJobsAsync(channel.Writer, cancellationTokenSource.Token);

		// Assert
		Assert.True(channel.Reader.Completion.IsCompleted);
	}

	[Theory, AutoMoqData]
	public async Task ProduceJobsAsync_CallsJobClaimingServiceAndDelayCalculator(
		Mock<ILogger<JobProducerService>> mockLogger,
		Mock<IDelayCalculatorService> mockDelayCalculatorService,
		Mock<IServiceScopeFactory> mockServiceScopeFactory,
		Mock<IServiceScope> mockServiceScope,
		Mock<IJobClaimingService> mockJobClaimingService,
		Guid workerId)
	{
		// Arrange
		var workerOptions = new WorkerOptions { WorkerId = workerId };
		mockServiceScopeFactory
			.Setup(x => x.CreateScope())
			.Returns(mockServiceScope.Object);

		mockServiceScope
			.Setup(x => x.ServiceProvider.GetService(typeof(IJobClaimingService)))
			.Returns(mockJobClaimingService.Object);

		var channel = Channel.CreateBounded<Job>(new BoundedChannelOptions(10));
		var cancellationTokenSource = new CancellationTokenSource();
		var cancellationToken = cancellationTokenSource.Token;
		var result = JobClaimingState.NoJobFound;

		mockJobClaimingService
			.Setup(x => x.ClaimAndEnqueueJobAsync(channel.Writer, workerId, cancellationToken))
			.ReturnsAsync(result);

		var expectedDelay = TimeSpan.FromMilliseconds(100);
		mockDelayCalculatorService
			.Setup(x => x.CalculateDelay(result, workerOptions))
			.Returns(expectedDelay);

		var jobProducerService = new JobProducerService(
			mockLogger.Object,
			workerOptions,
			mockDelayCalculatorService.Object,
			mockServiceScopeFactory.Object);

		var timeoutTask = Task.Delay(150);
		var serviceTask = jobProducerService.ProduceJobsAsync(channel.Writer, cancellationToken);

		await Task.Delay(50);
		cancellationTokenSource.Cancel();

		await Task.WhenAny(serviceTask, timeoutTask);

		mockServiceScopeFactory.Verify(x => x.CreateScope(), Times.AtLeastOnce);
		mockJobClaimingService.Verify(x => x.ClaimAndEnqueueJobAsync(channel.Writer, workerId, cancellationToken), Times.AtLeastOnce);
		mockDelayCalculatorService.Verify(x => x.CalculateDelay(result, workerOptions), Times.AtLeastOnce);
	}

	[Theory, AutoMoqData]
	public async Task ProduceJobsAsync_HandlesExceptionAndUsesErrorDelay(
		Mock<ILogger<JobProducerService>> mockLogger,
		Mock<IDelayCalculatorService> mockDelayCalculatorService,
		Mock<IServiceScopeFactory> mockServiceScopeFactory,
		Mock<IServiceScope> mockServiceScope,
		Mock<IJobClaimingService> mockJobClaimingService,
		Guid workerId)
	{
		// Arrange
		var workerOptions = new WorkerOptions { WorkerId = workerId };
		mockServiceScopeFactory
			.Setup(x => x.CreateScope())
			.Returns(mockServiceScope.Object);

		mockServiceScope
			.Setup(x => x.ServiceProvider.GetService(typeof(IJobClaimingService)))
			.Returns(mockJobClaimingService.Object);

		var channel = Channel.CreateBounded<Job>(new BoundedChannelOptions(10));
		var cancellationTokenSource = new CancellationTokenSource();
		var cancellationToken = cancellationTokenSource.Token;

		mockJobClaimingService
			.Setup(x => x.ClaimAndEnqueueJobAsync(channel.Writer, workerId, cancellationToken))
			.ThrowsAsync(new InvalidOperationException("Test exception"));

		var expectedErrorDelay = TimeSpan.FromSeconds(5);
		var errorState = JobClaimingState.ErrorOccurred;
		mockDelayCalculatorService
			.Setup(x => x.CalculateDelay(errorState, workerOptions))
			.Returns(expectedErrorDelay);

		var jobProducerService = new JobProducerService(
			mockLogger.Object,
			workerOptions,
			mockDelayCalculatorService.Object,
			mockServiceScopeFactory.Object);

		var timeoutTask = Task.Delay(200);
		var serviceTask = jobProducerService.ProduceJobsAsync(channel.Writer, cancellationToken);

		await Task.Delay(50);
		cancellationTokenSource.Cancel();

		await Task.WhenAny(serviceTask, timeoutTask);

		mockDelayCalculatorService.Verify(x => x.CalculateDelay(errorState, workerOptions), Times.AtLeastOnce);
	}
}
