using AutoFixture;
using AutoFixture.AutoMoq;

namespace AsyncEndpoints.UnitTests.TestSupport;

public class AsyncEndpointsFixture : Fixture
{
    public AsyncEndpointsFixture()
    {
        Customize(new AutoMoqCustomization());
    }
}