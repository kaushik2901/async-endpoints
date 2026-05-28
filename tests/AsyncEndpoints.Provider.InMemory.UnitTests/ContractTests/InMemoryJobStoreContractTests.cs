using AsyncEndpoints.Abstractions.UnitTests.ContractTests;
using AsyncEndpoints.Provider.InMemory.Storage;
using Moq;

namespace AsyncEndpoints.Provider.InMemory.UnitTests.ContractTests;

public class InMemoryJobStoreContractTests : JobStoreContractTestsBase
{
	protected override Abstractions.Storage.IJobStore CreateStore()
	{
		var mockDateTimeProvider = new Mock<TimeProvider>();
		mockDateTimeProvider.Setup(x => x.GetUtcNow()).Returns(() => DateTimeOffset.UtcNow);
		return new InMemoryJobStore(mockDateTimeProvider.Object);
	}
}
