using AsyncEndpoints.Abstractions.Infrastructure;
using AsyncEndpoints.Abstractions.UnitTests.ContractTests;
using AsyncEndpoints.Redis.Services;
using AsyncEndpoints.Redis.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace AsyncEndpoints.Redis.UnitTests.ContractTests;

public class RedisJobStoreContractTests : JobStoreContractTestsBase, IAsyncLifetime
{
	private static readonly string? RedisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING");
	private ConnectionMultiplexer? _multiplexer;
	private IDatabase? _database;

	protected override Abstractions.Storage.IJobStore CreateStore()
	{
		if (_database is null)
			throw new InvalidOperationException("Redis not initialized. Set REDIS_CONNECTION_STRING environment variable.");

		var mockDateTimeProvider = new Mock<IDateTimeProvider>();
		mockDateTimeProvider.Setup(x => x.UtcNow).Returns(() => DateTime.UtcNow);
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(() => DateTimeOffset.UtcNow);

		return new RedisJobStore(
			NullLogger<RedisJobStore>.Instance,
			_database,
			mockDateTimeProvider.Object,
			new JobHashConverter(),
			new RedisLuaScriptService(NullLogger<RedisLuaScriptService>.Instance)
		);
	}

	public async Task InitializeAsync()
	{
		if (string.IsNullOrWhiteSpace(RedisConnectionString))
			return;

		_multiplexer = await ConnectionMultiplexer.ConnectAsync(RedisConnectionString);
		_database = _multiplexer.GetDatabase();
		await FlushRedisAsync();
	}

	public async Task DisposeAsync()
	{
		if (_database is not null)
			await FlushRedisAsync();

		_multiplexer?.Dispose();
	}

	private async Task FlushRedisAsync()
	{
		if (_database is null) return;
		var endpoints = _multiplexer!.GetEndPoints();
		if (endpoints.Length > 0)
		{
			var server = _multiplexer.GetServer(endpoints[0]);
			await server.FlushDatabaseAsync(_database.Database);
		}
	}

	public override async Task Enqueue_ShouldCreateJob_AndReturnJobId()
	{
		if (string.IsNullOrWhiteSpace(RedisConnectionString))
			return;
		await base.Enqueue_ShouldCreateJob_AndReturnJobId();
	}

	public override async Task Dequeue_ShouldReturnNull_WhenQueueEmpty()
	{
		if (string.IsNullOrWhiteSpace(RedisConnectionString))
			return;
		await base.Dequeue_ShouldReturnNull_WhenQueueEmpty();
	}

	public override async Task Dequeue_ShouldReturnJob_WhenJobAvailable()
	{
		if (string.IsNullOrWhiteSpace(RedisConnectionString))
			return;
		await base.Dequeue_ShouldReturnJob_WhenJobAvailable();
	}

	public override async Task Dequeue_ShouldRespectChannel_FiltersByChannel()
	{
		if (string.IsNullOrWhiteSpace(RedisConnectionString))
			return;
		await base.Dequeue_ShouldRespectChannel_FiltersByChannel();
	}

	public override async Task Dequeue_ShouldReturnHighestPriorityJobFirst()
	{
		if (string.IsNullOrWhiteSpace(RedisConnectionString))
			return;
		await base.Dequeue_ShouldReturnHighestPriorityJobFirst();
	}

	public override async Task Dequeue_ShouldBeAtomic_TwoWorkersDontGetSameJob()
	{
		if (string.IsNullOrWhiteSpace(RedisConnectionString))
			return;
		await base.Dequeue_ShouldBeAtomic_TwoWorkersDontGetSameJob();
	}

	public override async Task Heartbeat_ShouldUpdateLastHeartbeat()
	{
		if (string.IsNullOrWhiteSpace(RedisConnectionString))
			return;
		await base.Heartbeat_ShouldUpdateLastHeartbeat();
	}

	public override async Task ReclaimStaleJobs_ShouldReclaimJobs_WithExpiredHeartbeat()
	{
		if (string.IsNullOrWhiteSpace(RedisConnectionString))
			return;
		await base.ReclaimStaleJobs_ShouldReclaimJobs_WithExpiredHeartbeat();
	}

	public override async Task ReclaimStaleJobs_ShouldNotReclaim_JobsWithRecentHeartbeat()
	{
		if (string.IsNullOrWhiteSpace(RedisConnectionString))
			return;
		await base.ReclaimStaleJobs_ShouldNotReclaim_JobsWithRecentHeartbeat();
	}

	public override async Task GetStatus_ShouldReturnCurrentStatus()
	{
		if (string.IsNullOrWhiteSpace(RedisConnectionString))
			return;
		await base.GetStatus_ShouldReturnCurrentStatus();
	}

	public override async Task GetStatus_ShouldReturnNull_WhenJobNotFound()
	{
		if (string.IsNullOrWhiteSpace(RedisConnectionString))
			return;
		await base.GetStatus_ShouldReturnNull_WhenJobNotFound();
	}

	public override async Task UpdateStatus_ShouldTransitionState_Valid()
	{
		if (string.IsNullOrWhiteSpace(RedisConnectionString))
			return;
		await base.UpdateStatus_ShouldTransitionState_Valid();
	}

	public override async Task UpdateStatus_ShouldReject_InvalidTransition()
	{
		if (string.IsNullOrWhiteSpace(RedisConnectionString))
			return;
		await base.UpdateStatus_ShouldReject_InvalidTransition();
	}

	public override async Task UpdateStatus_ShouldTransition_QueuedToProcessingToFailed()
	{
		if (string.IsNullOrWhiteSpace(RedisConnectionString))
			return;
		await base.UpdateStatus_ShouldTransition_QueuedToProcessingToFailed();
	}

	public override async Task UpdateStatus_ShouldTransition_FailedToQueued_Retry()
	{
		if (string.IsNullOrWhiteSpace(RedisConnectionString))
			return;
		await base.UpdateStatus_ShouldTransition_FailedToQueued_Retry();
	}

	public override async Task UpdateStatus_ShouldTransition_FailedToDeadLettered()
	{
		if (string.IsNullOrWhiteSpace(RedisConnectionString))
			return;
		await base.UpdateStatus_ShouldTransition_FailedToDeadLettered();
	}

	public override async Task UpdateStatus_ShouldTransition_ProcessingToQueued_InternalReclaim()
	{
		if (string.IsNullOrWhiteSpace(RedisConnectionString))
			return;
		await base.UpdateStatus_ShouldTransition_ProcessingToQueued_InternalReclaim();
	}

	public override async Task Enqueue_ShouldPreserve_Metadata()
	{
		if (string.IsNullOrWhiteSpace(RedisConnectionString))
			return;
		await base.Enqueue_ShouldPreserve_Metadata();
	}

	public override async Task Dequeue_ShouldRespectPartitionFilter()
	{
		if (string.IsNullOrWhiteSpace(RedisConnectionString))
			return;
		await base.Dequeue_ShouldRespectPartitionFilter();
	}
}
