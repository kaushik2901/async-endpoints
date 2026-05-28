using AsyncEndpoints.Abstractions.Infrastructure;
using AsyncEndpoints.Provider.InMemory.JobProcessing;
using Moq;

namespace AsyncEndpoints.UnitTests.ContractTests;

public class InMemoryJobStoreContractTests : JobStoreContractTestsBase
{
	protected override Abstractions.Storage.IJobStore CreateStore()
	{
		var mockDateTimeProvider = new Mock<IDateTimeProvider>();
		mockDateTimeProvider.Setup(x => x.UtcNow).Returns(() => DateTime.UtcNow);
		mockDateTimeProvider.Setup(x => x.DateTimeOffsetNow).Returns(() => DateTimeOffset.UtcNow);
		return new InMemoryJobStore(mockDateTimeProvider.Object);
	}
}
