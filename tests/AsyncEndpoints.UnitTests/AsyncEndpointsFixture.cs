using AutoFixture;
using AutoFixture.AutoMoq;

namespace AsyncEndpoints.UnitTests;

public class AsyncEndpointsFixture : Fixture
{
    public AsyncEndpointsFixture()
    {
        Customize(new AutoMoqCustomization());
    }
}