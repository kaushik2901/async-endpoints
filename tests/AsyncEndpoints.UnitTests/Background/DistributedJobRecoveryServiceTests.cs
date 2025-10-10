using AsyncEndpoints.Background;
using AsyncEndpoints.Configuration;
using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using Moq;

namespace AsyncEndpoints.UnitTests.Background;

public class DistributedJobRecoveryServiceTests
{
	/// <summary>
	/// Verifies that the DistributedJobRecoveryService can be constructed with valid dependencies without throwing an exception.
	/// This test ensures the constructor properly accepts and stores all required dependencies.
	/// </summary>
	[Theory, AutoMoqData]
	public void Constructor_Succeeds_WithValidDependencies(
		Mock<ILogger<DistributedJobRecoveryService>> mockLogger,
		Mock<IJobStore> mockJobStore,
		Mock<IDateTimeProvider> mockDateTimeProvider,
		AsyncEndpointsRecoveryConfiguration recoveryConfiguration)
	{
		// Act
		var service = new DistributedJobRecoveryService(
			mockLogger.Object,
			mockJobStore.Object,
			mockDateTimeProvider.Object,
			recoveryConfiguration);

		// Assert
		Assert.NotNull(service);
	}
}
