using AsyncEndpoints.Configuration;
using AsyncEndpoints.UnitTests.TestSupport;

namespace AsyncEndpoints.UnitTests;

public class AsyncEndpointsConfigurationsTests
{
	[Theory, AutoMoqData]
	public void DefaultValues_AreCorrect(
		AsyncEndpointsConfigurations config)
	{
		// Assert
		Assert.NotNull(config.WorkerConfigurations);
		Assert.NotNull(config.JobManagerConfigurations);
	}

	[Theory, AutoMoqData]
	public void WorkerConfigurations_CanBeSet(
		AsyncEndpointsConfigurations config,
		AsyncEndpointsWorkerConfigurations newWorkerConfig)
	{
		// Act
		config.WorkerConfigurations = newWorkerConfig;

		// Assert
		Assert.Same(newWorkerConfig, config.WorkerConfigurations);
	}

	[Theory, AutoMoqData]
	public void JobManagerConfiguration_CanBeSet(
		AsyncEndpointsConfigurations config,
		AsyncEndpointsJobManagerConfigurations newJobManagerConfigurations)
	{
		// Act
		config.JobManagerConfigurations = newJobManagerConfigurations;

		// Assert
		Assert.Same(newJobManagerConfigurations, config.JobManagerConfigurations);
	}
}

public class AsyncEndpointsWorkerConfigurationsTests
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var config = new AsyncEndpointsWorkerConfigurations();

		// Assert
		Assert.NotEqual(Guid.Empty, config.WorkerId);
		Assert.Equal(Environment.ProcessorCount, config.MaximumConcurrency);
		Assert.Equal(AsyncEndpointsConstants.DefaultPollingIntervalMs, config.PollingIntervalMs);
		Assert.Equal(AsyncEndpointsConstants.DefaultJobTimeoutMinutes, config.JobTimeoutMinutes);
		Assert.Equal(AsyncEndpointsConstants.DefaultBatchSize, config.BatchSize);
		Assert.Equal(AsyncEndpointsConstants.DefaultMaximumQueueSize, config.MaximumQueueSize);
	}

	[Theory, AutoMoqData]
	public void WorkerId_IsGeneratedOnConstruction(
		AsyncEndpointsWorkerConfigurations config1,
		AsyncEndpointsWorkerConfigurations config2)
	{
		// Assert
		Assert.NotEqual(Guid.Empty, config1.WorkerId);
		Assert.NotEqual(Guid.Empty, config2.WorkerId);
		Assert.NotEqual(config1.WorkerId, config2.WorkerId);
	}

	[Theory, AutoMoqData]
	public void Properties_CanBeSetAndRetrieved(
		Guid testWorkerId,
		int maximumConcurrency,
		int pollingIntervalMs,
		int jobTimeoutMinutes,
		int batchSize,
		int maximumQueueSize)
	{
		// Arrange
		var config = new AsyncEndpointsWorkerConfigurations();

		// Act
		config.WorkerId = testWorkerId;
		config.MaximumConcurrency = maximumConcurrency;
		config.PollingIntervalMs = pollingIntervalMs;
		config.JobTimeoutMinutes = jobTimeoutMinutes;
		config.BatchSize = batchSize;
		config.MaximumQueueSize = maximumQueueSize;

		// Assert
		Assert.Equal(testWorkerId, config.WorkerId);
		Assert.Equal(maximumConcurrency, config.MaximumConcurrency);
		Assert.Equal(pollingIntervalMs, config.PollingIntervalMs);
		Assert.Equal(jobTimeoutMinutes, config.JobTimeoutMinutes);
		Assert.Equal(batchSize, config.BatchSize);
		Assert.Equal(maximumQueueSize, config.MaximumQueueSize);
	}
}
