using AsyncEndpoints.Background;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.UnitTests.TestSupport;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.Logging;
using Moq;
using System.Threading.Channels;

namespace AsyncEndpoints.UnitTests.Background;

public class JobClaimingServiceTests
{
	[Theory, AutoMoqData]
	public void Constructor_Succeeds_WithValidDependencies(
		Mock<ILogger<JobClaimingService>> mockLogger,
		Mock<IJobManager> mockJobManager,
		Mock<IJobChannelEnqueuer> mockJobChannelEnqueuer)
	{
		// Act
		var service = new JobClaimingService(mockLogger.Object, mockJobManager.Object, mockJobChannelEnqueuer.Object);

		// Assert
		Assert.NotNull(service);
	}

	[Theory, AutoMoqData]
	public async Task ClaimAndEnqueueJobAsync_ReturnsErrorOccurred_WhenClaimingFails(
		Mock<ILogger<JobClaimingService>> mockLogger,
		Mock<IJobManager> mockJobManager,
		Mock<IJobChannelEnqueuer> mockJobChannelEnqueuer,
		Guid workerId,
		AsyncEndpointError error)
	{
		// Arrange
		var channel = Channel.CreateBounded<Job>(new BoundedChannelOptions(10));
		var cancellationToken = CancellationToken.None;
		
		mockJobManager
			.Setup(x => x.ClaimNextAvailableJob(workerId, cancellationToken))
			.ReturnsAsync(MethodResult<Job>.Failure(error));

		var service = new JobClaimingService(mockLogger.Object, mockJobManager.Object, mockJobChannelEnqueuer.Object);

		// Act
		var result = await service.ClaimAndEnqueueJobAsync(channel.Writer, workerId, cancellationToken);

		// Assert
		Assert.Equal(JobClaimingState.ErrorOccurred, result);
	}

	[Theory, AutoMoqData]
	public async Task ClaimAndEnqueueJobAsync_ReturnsNoJobFound_WhenNoJobAvailable(
		Mock<ILogger<JobClaimingService>> mockLogger,
		Mock<IJobManager> mockJobManager,
		Mock<IJobChannelEnqueuer> mockJobChannelEnqueuer,
		Guid workerId)
	{
		// Arrange
		var channel = Channel.CreateBounded<Job>(new BoundedChannelOptions(10));
		var cancellationToken = CancellationToken.None;
		
		mockJobManager
			.Setup(x => x.ClaimNextAvailableJob(workerId, cancellationToken))
			.ReturnsAsync(MethodResult<Job>.Success(default(Job)));

		var service = new JobClaimingService(mockLogger.Object, mockJobManager.Object, mockJobChannelEnqueuer.Object);

		// Act
		var result = await service.ClaimAndEnqueueJobAsync(channel.Writer, workerId, cancellationToken);

		// Assert
		Assert.Equal(JobClaimingState.NoJobFound, result);
	}

	[Theory, AutoMoqData]
	public async Task ClaimAndEnqueueJobAsync_ReturnsFailedToEnqueue_WhenChannelEnqueueFails(
		Mock<ILogger<JobClaimingService>> mockLogger,
		Mock<IJobManager> mockJobManager,
		Mock<IJobChannelEnqueuer> mockJobChannelEnqueuer,
		Guid workerId,
		Job job)
	{
		// Arrange
		var channel = Channel.CreateBounded<Job>(new BoundedChannelOptions(10));
		var cancellationToken = CancellationToken.None;
		
		mockJobManager
			.Setup(x => x.ClaimNextAvailableJob(workerId, cancellationToken))
			.ReturnsAsync(MethodResult<Job>.Success(job));
		
		mockJobChannelEnqueuer
			.Setup(x => x.Enqueue(channel.Writer, job, cancellationToken))
			.ReturnsAsync(false); // Simulate enqueue failure

		var service = new JobClaimingService(mockLogger.Object, mockJobManager.Object, mockJobChannelEnqueuer.Object);

		// Act
		var result = await service.ClaimAndEnqueueJobAsync(channel.Writer, workerId, cancellationToken);

		// Assert
		Assert.Equal(JobClaimingState.FailedToEnqueue, result);
	}

	[Theory, AutoMoqData]
	public async Task ClaimAndEnqueueJobAsync_ReturnsJobSuccessfullyEnqueued_WhenJobClaimedAndEnqueued(
		Mock<ILogger<JobClaimingService>> mockLogger,
		Mock<IJobManager> mockJobManager,
		Mock<IJobChannelEnqueuer> mockJobChannelEnqueuer,
		Guid workerId,
		Job job)
	{
		// Arrange
		var channel = Channel.CreateBounded<Job>(new BoundedChannelOptions(10));
		var cancellationToken = CancellationToken.None;
		
		mockJobManager
			.Setup(x => x.ClaimNextAvailableJob(workerId, cancellationToken))
			.ReturnsAsync(MethodResult<Job>.Success(job));
		
		mockJobChannelEnqueuer
			.Setup(x => x.Enqueue(channel.Writer, job, cancellationToken))
			.ReturnsAsync(true); // Simulate successful enqueue

		var service = new JobClaimingService(mockLogger.Object, mockJobManager.Object, mockJobChannelEnqueuer.Object);

		// Act
		var result = await service.ClaimAndEnqueueJobAsync(channel.Writer, workerId, cancellationToken);

		// Assert
		Assert.Equal(JobClaimingState.JobSuccessfullyEnqueued, result);
	}

	[Theory, AutoMoqData]
	public async Task ClaimAndEnqueueJobAsync_CallsJobManagerClaimNextAvailableJob_WithCorrectParameters(
		Mock<ILogger<JobClaimingService>> mockLogger,
		Mock<IJobManager> mockJobManager,
		Mock<IJobChannelEnqueuer> mockJobChannelEnqueuer,
		Guid workerId,
		Job job,
		CancellationToken cancellationToken)
	{
		// Arrange
		var channel = Channel.CreateBounded<Job>(new BoundedChannelOptions(10));
		
		mockJobManager
			.Setup(x => x.ClaimNextAvailableJob(workerId, cancellationToken))
			.ReturnsAsync(MethodResult<Job>.Success(job));
		
		mockJobChannelEnqueuer
			.Setup(x => x.Enqueue(channel.Writer, job, cancellationToken))
			.ReturnsAsync(true);

		var service = new JobClaimingService(mockLogger.Object, mockJobManager.Object, mockJobChannelEnqueuer.Object);

		// Act
		await service.ClaimAndEnqueueJobAsync(channel.Writer, workerId, cancellationToken);

		// Assert
		mockJobManager.Verify(x => x.ClaimNextAvailableJob(workerId, cancellationToken), Times.Once);
	}

	[Theory, AutoMoqData]
	public async Task ClaimAndEnqueueJobAsync_CallsJobChannelEnqueuer_WithCorrectParameters(
		Mock<ILogger<JobClaimingService>> mockLogger,
		Mock<IJobManager> mockJobManager,
		Mock<IJobChannelEnqueuer> mockJobChannelEnqueuer,
		Guid workerId,
		Job job,
		CancellationToken cancellationToken)
	{
		// Arrange
		var channel = Channel.CreateBounded<Job>(new BoundedChannelOptions(10));
		
		mockJobManager
			.Setup(x => x.ClaimNextAvailableJob(workerId, cancellationToken))
			.ReturnsAsync(MethodResult<Job>.Success(job));
		
		mockJobChannelEnqueuer
			.Setup(x => x.Enqueue(channel.Writer, job, cancellationToken))
			.ReturnsAsync(true);

		var service = new JobClaimingService(mockLogger.Object, mockJobManager.Object, mockJobChannelEnqueuer.Object);

		// Act
		await service.ClaimAndEnqueueJobAsync(channel.Writer, workerId, cancellationToken);

		// Assert
		mockJobChannelEnqueuer.Verify(x => x.Enqueue(channel.Writer, job, cancellationToken), Times.Once);
	}
}