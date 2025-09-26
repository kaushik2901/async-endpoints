namespace AsyncEndpoints.UnitTests;

public class AsyncEndpointsConfigurationsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Act
        var config = new AsyncEndpointsConfigurations();

        // Assert
        Assert.Equal(AsyncEndpointsConstants.MaximumRetries, config.MaximumRetries);
        Assert.NotNull(config.WorkerConfigurations);
    }

    [Fact]
    public void MaximumRetries_CanBeSet()
    {
        // Arrange
        var config = new AsyncEndpointsConfigurations();

        // Act
        config.MaximumRetries = 5;

        // Assert
        Assert.Equal(5, config.MaximumRetries);
    }

    [Fact]
    public void WorkerConfigurations_CanBeSet()
    {
        // Arrange
        var config = new AsyncEndpointsConfigurations();
        var newWorkerConfig = new AsyncEndpointsWorkerConfigurations { MaximumConcurrency = 8 };

        // Act
        config.WorkerConfigurations = newWorkerConfig;

        // Assert
        Assert.Same(newWorkerConfig, config.WorkerConfigurations);
        Assert.Equal(8, config.WorkerConfigurations.MaximumConcurrency);
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

    [Fact]
    public void WorkerId_IsGeneratedOnConstruction()
    {
        // Act
        var config1 = new AsyncEndpointsWorkerConfigurations();
        var config2 = new AsyncEndpointsWorkerConfigurations();

        // Assert
        Assert.NotEqual(Guid.Empty, config1.WorkerId);
        Assert.NotEqual(Guid.Empty, config2.WorkerId);
        Assert.NotEqual(config1.WorkerId, config2.WorkerId);
    }

    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var config = new AsyncEndpointsWorkerConfigurations();
        var testWorkerId = Guid.NewGuid();

        // Act
        config.WorkerId = testWorkerId;
        config.MaximumConcurrency = 10;
        config.PollingIntervalMs = 2000;
        config.JobTimeoutMinutes = 60;
        config.BatchSize = 10;
        config.MaximumQueueSize = 100;

        // Assert
        Assert.Equal(testWorkerId, config.WorkerId);
        Assert.Equal(10, config.MaximumConcurrency);
        Assert.Equal(2000, config.PollingIntervalMs);
        Assert.Equal(60, config.JobTimeoutMinutes);
        Assert.Equal(10, config.BatchSize);
        Assert.Equal(100, config.MaximumQueueSize);
    }
}