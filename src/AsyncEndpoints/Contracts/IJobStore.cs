using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Entities;
using AsyncEndpoints.Utilities;

namespace AsyncEndpoints.Contracts;

/// <summary>
/// Defines a contract for storing and managing asynchronous jobs.
/// Provides methods for creating, retrieving, updating, and querying jobs.
/// </summary>
public interface IJobStore
{
	/// <summary>
	/// Creates a new job in the store
	/// </summary>
	Task<MethodResult> CreateJob(Job job, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves a job by its unique identifier
	/// </summary>
	Task<MethodResult<Job>> GetJobById(Guid id, CancellationToken cancellationToken);

	/// <summary>
	/// Updates the complete job entity
	/// </summary>
	Task<MethodResult> UpdateJob(Job job, CancellationToken cancellationToken);

	/// <summary>
	/// Atomically claims available jobs for a specific worker
	/// </summary>
	Task<MethodResult<List<Job>>> ClaimJobsForWorker(Guid workerId, int maxClaimCount, CancellationToken cancellationToken);
}
