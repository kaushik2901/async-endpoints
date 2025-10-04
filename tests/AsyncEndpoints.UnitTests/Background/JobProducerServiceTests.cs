using System.Threading.Channels;
using AsyncEndpoints.Background;
using AsyncEndpoints.Configuration;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.UnitTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AsyncEndpoints.UnitTests.Background;

public class JobProducerServiceTests
{
	[Theory, AutoMoqData]
	public void Constructor_Succeeds_WithValidDependencies(
		Mock<ILogger<JobProducerService>> mockLogger,
		Mock<IOptions<AsyncEndpointsConfigurations>> mockConfigurations,
		Mock<IDelayCalculatorService> mockDelayCalculatorService,
		Mock<IServiceScopeFactory> mockServiceScopeFactory,
		AsyncEndpointsConfigurations configurations)
	{
		// Arrange
		mockConfigurations
			.Setup(x => x.Value)
			.Returns(configurations);

		// Act
		var service = new JobProducerService(
			mockLogger.Object, 
			mockConfigurations.Object, 
			mockDelayCalculatorService.Object, 
			mockServiceScopeFactory.Object);

		// Assert
		Assert.NotNull(service);
	}

	[Theory, AutoMoqData]
	public async Task ProduceJobsAsync_CompletesChannel_WhenCancellationRequested(
		Mock<ILogger<JobProducerService>> mockLogger,
		Mock<IOptions<AsyncEndpointsConfigurations>> mockConfigurations,
		Mock<IDelayCalculatorService> mockDelayCalculatorService,
		Mock<IServiceScopeFactory> mockServiceScopeFactory,
		Mock<IServiceScope> mockServiceScope,
		Mock<IJobClaimingService> mockJobClaimingService)
	{
		// Arrange
		var configurations = new AsyncEndpointsConfigurations { WorkerConfigurations = new AsyncEndpointsWorkerConfigurations() };
		mockConfigurations.Setup(x => x.Value).Returns(configurations);
		
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
			mockConfigurations.Object, 
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
		Mock<IOptions<AsyncEndpointsConfigurations>> mockConfigurations,
		Mock<IDelayCalculatorService> mockDelayCalculatorService,
		Mock<IServiceScopeFactory> mockServiceScopeFactory,
		Mock<IServiceScope> mockServiceScope,
		Mock<IJobClaimingService> mockJobClaimingService,
		Guid workerId)
	{
		// Arrange
		var workerConfigurations = new AsyncEndpointsWorkerConfigurations { WorkerId = workerId };
		var configurations = new AsyncEndpointsConfigurations { WorkerConfigurations = workerConfigurations };
		mockConfigurations.Setup(x => x.Value).Returns(configurations);
		
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
		
		// Setup the job claiming service to return a specific result
		mockJobClaimingService
			.Setup(x => x.ClaimAndEnqueueJobAsync(channel.Writer, workerId, cancellationToken))
			.ReturnsAsync(result);
		
		// Setup the delay calculator to return a specific delay
		var expectedDelay = TimeSpan.FromMilliseconds(100);
		mockDelayCalculatorService
			.Setup(x => x.CalculateDelay(result, workerConfigurations))
			.Returns(expectedDelay);

		var jobProducerService = new JobProducerService(
			mockLogger.Object, 
			mockConfigurations.Object, 
			mockDelayCalculatorService.Object, 
			mockServiceScopeFactory.Object);

		// Act & Assert - Use a short timeout to prevent hanging
		var timeoutTask = Task.Delay(150); // Give it a bit more time than the delay
		var serviceTask = jobProducerService.ProduceJobsAsync(channel.Writer, cancellationToken);
		
		// Cancel after a short time to prevent infinite loop
		await Task.Delay(50);
		cancellationTokenSource.Cancel();
		
		await Task.WhenAny(serviceTask, timeoutTask);
		
		// Verify the service was called at least once
		mockServiceScopeFactory.Verify(x => x.CreateScope(), Times.AtLeastOnce);
		mockJobClaimingService.Verify(x => x.ClaimAndEnqueueJobAsync(channel.Writer, workerId, cancellationToken), Times.AtLeastOnce);
		mockDelayCalculatorService.Verify(x => x.CalculateDelay(result, workerConfigurations), Times.AtLeastOnce);
	}

	[Theory, AutoMoqData]
	public async Task ProduceJobsAsync_HandlesExceptionAndUsesErrorDelay(
		Mock<ILogger<JobProducerService>> mockLogger,
		Mock<IOptions<AsyncEndpointsConfigurations>> mockConfigurations,
		Mock<IDelayCalculatorService> mockDelayCalculatorService,
		Mock<IServiceScopeFactory> mockServiceScopeFactory,
		Mock<IServiceScope> mockServiceScope,
		Mock<IJobClaimingService> mockJobClaimingService,
		Guid workerId)
	{
		// Arrange
		var workerConfigurations = new AsyncEndpointsWorkerConfigurations { WorkerId = workerId };
		var configurations = new AsyncEndpointsConfigurations { WorkerConfigurations = workerConfigurations };
		mockConfigurations.Setup(x => x.Value).Returns(configurations);
		
		mockServiceScopeFactory
			.Setup(x => x.CreateScope())
			.Returns(mockServiceScope.Object);
		
		mockServiceScope
			.Setup(x => x.ServiceProvider.GetService(typeof(IJobClaimingService)))
			.Returns(mockJobClaimingService.Object);
		
		var channel = Channel.CreateBounded<Job>(new BoundedChannelOptions(10));
		var cancellationTokenSource = new CancellationTokenSource();
		var cancellationToken = cancellationTokenSource.Token;
		
		// Setup the job claiming service to throw an exception
		mockJobClaimingService
			.Setup(x => x.ClaimAndEnqueueJobAsync(channel.Writer, workerId, cancellationToken))
			.ThrowsAsync(new InvalidOperationException("Test exception"));
		
		// Setup the delay calculator to return a specific delay for error state
		var expectedErrorDelay = TimeSpan.FromSeconds(5);
		var errorState = JobClaimingState.ErrorOccurred;
		mockDelayCalculatorService
			.Setup(x => x.CalculateDelay(errorState, workerConfigurations))
			.Returns(expectedErrorDelay);

		var jobProducerService = new JobProducerService(
			mockLogger.Object, 
			mockConfigurations.Object, 
			mockDelayCalculatorService.Object, 
			mockServiceScopeFactory.Object);

		// Act & Assert - Use a short timeout to prevent hanging
		var timeoutTask = Task.Delay(200);
		var serviceTask = jobProducerService.ProduceJobsAsync(channel.Writer, cancellationToken);
		
		// Cancel after a short time to prevent infinite loop
		await Task.Delay(50);
		cancellationTokenSource.Cancel();
		
		await Task.WhenAny(serviceTask, timeoutTask);
		
		// Verify the delay calculator was called with the error state
		mockDelayCalculatorService.Verify(x => x.CalculateDelay(errorState, workerConfigurations), Times.AtLeastOnce);
	}
}
