using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;

namespace AsyncEndpoints.AspNetCore.UnitTests.TestSupport;

public class AutoMoqDataAttribute : AutoDataAttribute
{
	public AutoMoqDataAttribute() : base(() => new Fixture().Customize(new AutoMoqCustomization()))
	{
	}
}
