using AsyncEndpoints.Abstractions.UnitTests.ContractTests;
using AsyncEndpoints.Core.Storage;
using Moq;

namespace AsyncEndpoints.Core.UnitTests.Storage;

public class InMemoryJobStoreContractTests : JobStoreContractTestsBase
{
	protected override Abstractions.Storage.IJobStore CreateStore()
	{
		var mockDateTimeProvider = new Mock<TimeProvider>();
		mockDateTimeProvider.Setup(x => x.GetUtcNow()).Returns(() => DateTimeOffset.UtcNow);
		return new InMemoryJobStore(mockDateTimeProvider.Object);
	}
}
