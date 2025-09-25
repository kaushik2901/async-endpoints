using AutoFixture.Xunit2;
using AsyncEndpoints;

namespace AsyncEndpoints.UnitTests;

public class AsyncEndpointsConfigurationsTests
{
    [Fact]
    public void DefaultConstructor_InitializesWithDefaultValues()
    {
        // Act
        var config = new AsyncEndpointsConfigurations();
        
        // Assert
        Assert.Equal(3, config.MaximumRetries); // matches AsyncEndpointsConstants.MaximumRetries
        Assert.NotNull(config.WorkerConfigurations);
    }
    
    [Theory, AutoMoqData]
    public void AutoFixtureCanCreateConfigurations(AsyncEndpointsConfigurations config)
    {
        // Assert
        Assert.NotNull(config);
        Assert.NotNull(config.WorkerConfigurations);
    }
}