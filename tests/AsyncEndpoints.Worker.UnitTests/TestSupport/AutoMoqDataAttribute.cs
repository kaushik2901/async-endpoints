using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;

namespace AsyncEndpoints.Worker.UnitTests.TestSupport;

public class AutoMoqDataAttribute : AutoDataAttribute
{
	public AutoMoqDataAttribute() : base(() => new Fixture().Customize(new AutoMoqCustomization()))
	{
	}
}
