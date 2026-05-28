using AsyncEndpoints.Abstractions.Partitioning;
using AsyncEndpoints.Core.Partitioning;
using Moq;

namespace AsyncEndpoints.Core.UnitTests;

public class PartitionManagerTests
{
	[Fact]
	public async Task AssignWorkerToPartition_ReturnsPartition()
	{
		var mockAssigner = new Mock<IPartitionAssigner>();
		mockAssigner.Setup(a => a.AcquirePartitionAsync("worker-1", "default", It.IsAny<CancellationToken>()))
			.ReturnsAsync(3);

		var manager = new PartitionManager(mockAssigner.Object);

		var partition = await manager.AssignWorkerToPartitionAsync("worker-1", "default");

		Assert.NotNull(partition);
		Assert.Equal(3, partition);
	}

	[Fact]
	public async Task GetAssignedPartitions_ReturnsCorrectSet()
	{
		var mockAssigner = new Mock<IPartitionAssigner>();
		mockAssigner.Setup(a => a.AcquirePartitionAsync("worker-1", "default", It.IsAny<CancellationToken>()))
			.ReturnsAsync(1);
		mockAssigner.Setup(a => a.AcquirePartitionAsync("worker-2", "default", It.IsAny<CancellationToken>()))
			.ReturnsAsync(2);

		var manager = new PartitionManager(mockAssigner.Object);

		await manager.AssignWorkerToPartitionAsync("worker-1", "default");
		await manager.AssignWorkerToPartitionAsync("worker-2", "default");

		var worker1Partitions = manager.GetAssignedPartitions("worker-1");
		var worker2Partitions = manager.GetAssignedPartitions("worker-2");

		Assert.Single(worker1Partitions);
		Assert.Contains(1, worker1Partitions);
		Assert.Single(worker2Partitions);
		Assert.Contains(2, worker2Partitions);
	}

	[Fact]
	public async Task ReleasePartition_RemovesPartitionFromWorker()
	{
		var mockAssigner = new Mock<IPartitionAssigner>();
		mockAssigner.Setup(a => a.AcquirePartitionAsync("worker-1", "default", It.IsAny<CancellationToken>()))
			.ReturnsAsync(1);

		var manager = new PartitionManager(mockAssigner.Object);

		await manager.AssignWorkerToPartitionAsync("worker-1", "default");
		await manager.ReleasePartitionAsync(1, "worker-1");

		var partitions = manager.GetAssignedPartitions("worker-1");
		Assert.Empty(partitions);
	}
}
