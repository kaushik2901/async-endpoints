using System.Threading.Channels;
using AsyncEndpoints.Background;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.UnitTests.TestSupport;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.UnitTests.Background;

public class JobClaimingServiceTests
{
	/// <summary>
	/// Verifies that the JobClaimingService can be constructed with valid dependencies without throwing an exception.
	/// This test ensures the constructor properly accepts and stores all required dependencies.
	/// </summary>
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

	/// <summary>
	/// Verifies that when job claiming fails, the JobClaimingService returns the ErrorOccurred state.
	/// This ensures proper error handling when the job manager fails to claim a job.
	/// </summary>
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

	/// <summary>
	/// Verifies that when no job is available for claiming, the JobClaimingService returns the NoJobFound state.
	/// This ensures proper handling of empty queues during the job claiming process.
	/// </summary>
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

	/// <summary>
	/// Verifies that when job enqueue to the channel fails, the JobClaimingService returns the FailedToEnqueue state.
	/// This ensures proper error handling when the channel is full or unavailable.
	/// </summary>
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

	/// <summary>
	/// Verifies that when a job is successfully claimed and enqueued, the JobClaimingService returns the JobSuccessfullyEnqueued state.
	/// This ensures the complete successful workflow is handled correctly.
	/// </summary>
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

	/// <summary>
	/// Verifies that the JobClaimingService calls the JobManager's ClaimNextAvailableJob method with the correct parameters.
	/// This ensures the job claiming service properly delegates to the job manager for job claiming.
	/// </summary>
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

	/// <summary>
	/// Verifies that the JobClaimingService calls the JobChannelEnqueuer's Enqueue method with the correct parameters.
	/// This ensures the job claiming service properly delegates to the channel enqueuer for job placement.
	/// </summary>
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
