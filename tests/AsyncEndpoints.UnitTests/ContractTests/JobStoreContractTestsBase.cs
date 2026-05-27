using AsyncEndpoints.Abstractions.Jobs;

namespace AsyncEndpoints.UnitTests.ContractTests;

public abstract class JobStoreContractTestsBase
{
    protected abstract Abstractions.Storage.IJobStore CreateStore();

    [Fact]
    public virtual async Task Enqueue_ShouldCreateJob_AndReturnJobId()
    {
        var store = CreateStore();
        var descriptor = new JobDescriptor("TestJob", "{}", Channel: "test");

        var jobId = await store.EnqueueAsync(descriptor);

        Assert.NotEqual(Guid.Empty, jobId);
    }

    [Fact]
    public virtual async Task Dequeue_ShouldReturnNull_WhenQueueEmpty()
    {
        var store = CreateStore();

        var result = await store.DequeueAsync("nonexistent", null);

        Assert.Null(result);
    }

    [Fact]
    public virtual async Task Dequeue_ShouldReturnJob_WhenJobAvailable()
    {
        var store = CreateStore();
        var descriptor = new JobDescriptor("TestJob", "{}", Channel: "test");
        var jobId = await store.EnqueueAsync(descriptor);

        var result = await store.DequeueAsync("test", null);

        Assert.NotNull(result);
        Assert.Equal(jobId, result.JobId);
        Assert.Equal("TestJob", result.JobName);
        Assert.Equal(JobStatus.Processing, result.Status);
        Assert.NotNull(result.StartedAt);
        Assert.NotNull(result.LastHeartbeat);
    }

    [Fact]
    public virtual async Task Dequeue_ShouldRespectChannel_FiltersByChannel()
    {
        var store = CreateStore();
        await store.EnqueueAsync(new JobDescriptor("JobA", "{}", Channel: "channel-a"));
        await store.EnqueueAsync(new JobDescriptor("JobB", "{}", Channel: "channel-b"));

        var resultA = await store.DequeueAsync("channel-a", null);
        var resultB = await store.DequeueAsync("channel-b", null);

        Assert.NotNull(resultA);
        Assert.Equal("JobA", resultA.JobName);
        Assert.NotNull(resultB);
        Assert.Equal("JobB", resultB.JobName);
    }

    [Fact]
    public virtual async Task Dequeue_ShouldReturnHighestPriorityJobFirst()
    {
        var store = CreateStore();
        await store.EnqueueAsync(new JobDescriptor("Low", "{}", Channel: "test", Priority: 1));
        await store.EnqueueAsync(new JobDescriptor("High", "{}", Channel: "test", Priority: 10));

        var first = await store.DequeueAsync("test", null);
        var second = await store.DequeueAsync("test", null);

        Assert.NotNull(first);
        Assert.Equal("High", first.JobName);
        Assert.NotNull(second);
        Assert.Equal("Low", second.JobName);
    }

    [Fact]
    public virtual async Task Dequeue_ShouldBeAtomic_TwoWorkersDontGetSameJob()
    {
        var store = CreateStore();
        await store.EnqueueAsync(new JobDescriptor("TestJob", "{}", Channel: "test"));

        var task1 = store.DequeueAsync("test", null);
        var task2 = store.DequeueAsync("test", null);

        var results = await Task.WhenAll(task1, task2);

        Assert.NotNull(results[0]);
        Assert.True(results[1] is null || results[1]!.JobId != results[0]!.JobId,
            "Two workers should not get the same job");
    }

    [Fact]
    public virtual async Task Heartbeat_ShouldUpdateLastHeartbeat()
    {
        var store = CreateStore();
        var descriptor = new JobDescriptor("TestJob", "{}", Channel: "test");
        var jobId = await store.EnqueueAsync(descriptor);

        var dequeued = await store.DequeueAsync("test", null);
        Assert.NotNull(dequeued);
        var originalHeartbeat = dequeued.LastHeartbeat;

        await Task.Delay(10);
        await store.HeartbeatAsync(jobId);

        var status = await store.GetStatusAsync(jobId);
        Assert.NotNull(status);
        Assert.True(status.LastHeartbeat > originalHeartbeat,
            "Heartbeat should be updated to a later time");
    }

    [Fact]
    public virtual async Task ReclaimStaleJobs_ShouldReclaimJobs_WithExpiredHeartbeat()
    {
        var store = CreateStore();
        var descriptor = new JobDescriptor("TestJob", "{}", Channel: "test");
        var jobId = await store.EnqueueAsync(descriptor);

        await store.DequeueAsync("test", null);

        var reclaimed = await store.ReclaimStaleJobsAsync(TimeSpan.FromSeconds(-1));

        Assert.True(reclaimed > 0, "At least one stale job should be reclaimed");
    }

    [Fact]
    public virtual async Task ReclaimStaleJobs_ShouldNotReclaim_JobsWithRecentHeartbeat()
    {
        var store = CreateStore();
        var descriptor = new JobDescriptor("TestJob", "{}", Channel: "test");
        var jobId = await store.EnqueueAsync(descriptor);

        await store.DequeueAsync("test", null);
        await store.HeartbeatAsync(jobId);

        var reclaimed = await store.ReclaimStaleJobsAsync(TimeSpan.FromHours(1));

        Assert.Equal(0, reclaimed);
    }

    [Fact]
    public virtual async Task GetStatus_ShouldReturnCurrentStatus()
    {
        var store = CreateStore();
        var descriptor = new JobDescriptor("TestJob", "{}", Channel: "test");
        var jobId = await store.EnqueueAsync(descriptor);

        var status = await store.GetStatusAsync(jobId);

        Assert.NotNull(status);
        Assert.Equal(jobId, status.JobId);
        Assert.Equal(JobStatus.Queued, status.Status);
    }

    [Fact]
    public virtual async Task GetStatus_ShouldReturnNull_WhenJobNotFound()
    {
        var store = CreateStore();

        var status = await store.GetStatusAsync(Guid.NewGuid());

        Assert.Null(status);
    }

    [Fact]
    public virtual async Task UpdateStatus_ShouldTransitionState_Valid()
    {
        var store = CreateStore();
        var descriptor = new JobDescriptor("TestJob", "{}", Channel: "test");
        var jobId = await store.EnqueueAsync(descriptor);

        await store.DequeueAsync("test", null);
        await store.UpdateStatusAsync(jobId, JobStatus.Completed, "done");

        var status = await store.GetStatusAsync(jobId);
        Assert.NotNull(status);
        Assert.Equal(JobStatus.Completed, status.Status);
        Assert.Equal("done", status.Result);
        Assert.NotNull(status.CompletedAt);
    }

    [Fact]
    public virtual async Task UpdateStatus_ShouldReject_InvalidTransition()
    {
        var store = CreateStore();
        var jobId = await store.EnqueueAsync(new JobDescriptor("TestJob", "{}", Channel: "test"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.UpdateStatusAsync(jobId, JobStatus.Completed));
    }

    [Fact]
    public virtual async Task UpdateStatus_ShouldTransition_QueuedToProcessingToFailed()
    {
        var store = CreateStore();
        var jobId = await store.EnqueueAsync(new JobDescriptor("TestJob", "{}", Channel: "test"));

        await store.DequeueAsync("test", null);
        await store.UpdateStatusAsync(jobId, JobStatus.Failed, "error occurred");

        var status = await store.GetStatusAsync(jobId);
        Assert.Equal(JobStatus.Failed, status!.Status);
        Assert.Equal("error occurred", status.Result);
    }

    [Fact]
    public virtual async Task UpdateStatus_ShouldTransition_FailedToQueued_Retry()
    {
        var store = CreateStore();
        var jobId = await store.EnqueueAsync(new JobDescriptor("TestJob", "{}", Channel: "test"));

        await store.DequeueAsync("test", null);
        await store.UpdateStatusAsync(jobId, JobStatus.Failed);
        await store.UpdateStatusAsync(jobId, JobStatus.Queued);

        var status = await store.GetStatusAsync(jobId);
        Assert.Equal(JobStatus.Queued, status!.Status);
    }

    [Fact]
    public virtual async Task UpdateStatus_ShouldTransition_FailedToDeadLettered()
    {
        var store = CreateStore();
        var jobId = await store.EnqueueAsync(new JobDescriptor("TestJob", "{}", Channel: "test"));

        await store.DequeueAsync("test", null);
        await store.UpdateStatusAsync(jobId, JobStatus.Failed);
        await store.UpdateStatusAsync(jobId, JobStatus.DeadLettered);

        var status = await store.GetStatusAsync(jobId);
        Assert.Equal(JobStatus.DeadLettered, status!.Status);
    }

    [Fact]
    public virtual async Task UpdateStatus_ShouldTransition_ProcessingToQueued_InternalReclaim()
    {
        var store = CreateStore();
        var jobId = await store.EnqueueAsync(new JobDescriptor("TestJob", "{}", Channel: "test"));

        await store.DequeueAsync("test", null);
        await store.UpdateStatusAsync(jobId, JobStatus.Queued);

        var status = await store.GetStatusAsync(jobId);
        Assert.Equal(JobStatus.Queued, status!.Status);
        Assert.Null(status.StartedAt);
    }

    [Fact]
    public virtual async Task Enqueue_ShouldPreserve_Metadata()
    {
        var store = CreateStore();
        var metadata = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
        var descriptor = new JobDescriptor("TestJob", "{}", Channel: "test", Metadata: metadata);

        var jobId = await store.EnqueueAsync(descriptor);

        var status = await store.GetStatusAsync(jobId);
        Assert.NotNull(status);
        Assert.NotNull(status.Metadata);
        Assert.Equal("value1", status.Metadata["key1"]);
        Assert.Equal("value2", status.Metadata["key2"]);
    }

    [Fact]
    public virtual async Task Dequeue_ShouldRespectPartitionFilter()
    {
        var store = CreateStore();
        await store.EnqueueAsync(new JobDescriptor("Job1", "{}", Channel: "test", PartitionKey: "p1"));
        await store.EnqueueAsync(new JobDescriptor("Job2", "{}", Channel: "test", PartitionKey: "p2"));

        var partition1 = new HashSet<int> { Math.Abs("p1".GetHashCode(StringComparison.Ordinal)) % 100 };
        var fromP1 = await store.DequeueAsync("test", partition1);

        Assert.NotNull(fromP1);
        Assert.Equal("Job1", fromP1.JobName);
    }
}
