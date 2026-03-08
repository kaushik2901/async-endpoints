using System;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncEndpoints.JobProcessing;

public class InMemoryJobRecoveryService : IJobRecoveryService
{
	public bool SupportsJobRecovery => false; // In-memory store doesn't support recovery

	public Task<int> RecoverStuckJobs(long timeoutUnixTime, int maxRetries, CancellationToken cancellationToken)
	{
		throw new NotSupportedException("In-memory job store does not support job recovery operations.");
	}
}
