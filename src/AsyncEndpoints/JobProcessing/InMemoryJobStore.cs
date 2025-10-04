using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.JobProcessing;

/// <summary>
/// An in-memory implementation of the IJobStore interface.
/// This implementation stores jobs in a thread-safe concurrent dictionary and is suitable for development or single-instance deployments.
/// </summary>
public class InMemoryJobStore(ILogger<InMemoryJobStore> logger, IDateTimeProvider dateTimeProvider) : IJobStore
{
	private readonly ILogger<InMemoryJobStore> _logger = logger;
	private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
	private readonly ConcurrentDictionary<Guid, Job> jobs = new();

	/// <summary>
	/// Creates a new job in the store.
	/// </summary>
	/// <param name="job">The job to create.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A <see cref="MethodResult"/> indicating the result of the operation.</returns>
	public Task<MethodResult> CreateJob(Job job, CancellationToken cancellationToken)
	{
		try
		{
			if (job == null)
			{
				_logger.LogWarning("Attempted to create null job");
				return Task.FromResult(MethodResult.Failure(
					AsyncEndpointError.FromCode("INVALID_JOB", "Job cannot be null")));
			}

			if (job.Id == Guid.Empty)
			{
				_logger.LogWarning("Attempted to create job with empty ID");
				return Task.FromResult(MethodResult.Failure(
					AsyncEndpointError.FromCode("INVALID_JOB_ID", "Job ID cannot be empty")));
			}

			if (cancellationToken.IsCancellationRequested)
			{
				_logger.LogDebug("Job create operation cancelled");
				return Task.FromCanceled<MethodResult>(cancellationToken);
			}

			if (!jobs.TryAdd(job.Id, job))
			{
				_logger.LogError("Failed to create job with ID {JobId}", job.Id);
				return Task.FromResult(MethodResult.Failure(
					AsyncEndpointError.FromCode("JOB_CREATE_FAILED", $"Failed to create job with ID {job.Id}")));
			}

			_logger.LogInformation("Created job {JobId} with name {JobName}", job.Id, job.Name);
			return Task.FromResult(MethodResult.Success());
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error creating job: {JobName}", job?.Name);
			return Task.FromResult(MethodResult.Failure(
				AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error creating job: {ex.Message}", ex)));
		}
	}

	/// <summary>
	/// Retrieves a job by its unique identifier.
	/// </summary>
	/// <param name="id">The unique identifier of the job to retrieve.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A <see cref="MethodResult{T}"/> containing the job if found, or an error if not found.</returns>
	public Task<MethodResult<Job>> GetJobById(Guid id, CancellationToken cancellationToken)
	{
		try
		{
			if (id == Guid.Empty)
			{
				_logger.LogWarning("Attempted to retrieve job with empty ID");
				return Task.FromResult(MethodResult<Job>.Failure(
					AsyncEndpointError.FromCode("INVALID_JOB_ID", "Job ID cannot be empty")));
			}

			if (cancellationToken.IsCancellationRequested)
			{
				_logger.LogDebug("Job get operation cancelled for ID {JobId}", id);
				return Task.FromCanceled<MethodResult<Job>>(cancellationToken);
			}

			jobs.TryGetValue(id, out var job);

			if (job == null)
			{
				_logger.LogWarning("Job not found with Id {JobId} from store", id);
				return Task.FromResult(MethodResult<Job>.Failure(
					AsyncEndpointError.FromCode("JOB_NOT_FOUND", $"Job with ID {id} not found")));
			}

			return Task.FromResult(MethodResult<Job>.Success(job));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error retrieving job: {JobId}", id);
			return Task.FromResult(MethodResult<Job>.Failure(
				AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error retrieving job: {ex.Message}", ex)));
		}
	}

	/// <summary>
	/// Updates the complete job entity.
	/// </summary>
	/// <param name="job">The updated job entity.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A <see cref="MethodResult"/> indicating the result of the operation.</returns>
	public Task<MethodResult> UpdateJob(Job job, CancellationToken cancellationToken)
	{
		try
		{
			if (job == null)
			{
				_logger.LogWarning("Attempted to update null job");
				return Task.FromResult(MethodResult.Failure(
					AsyncEndpointError.FromCode("INVALID_JOB", "Job cannot be null")));
			}

			if (job.Id == Guid.Empty)
			{
				_logger.LogWarning("Attempted to update job with empty ID");
				return Task.FromResult(MethodResult.Failure(
					AsyncEndpointError.FromCode("INVALID_JOB_ID", "Job ID cannot be empty")));
			}

			if (cancellationToken.IsCancellationRequested)
			{
				_logger.LogDebug("Job update operation cancelled for ID {JobId}", job.Id);
				return Task.FromCanceled<MethodResult>(cancellationToken);
			}

			if (!jobs.TryGetValue(job.Id, out var existingJob))
			{
				_logger.LogWarning("Attempted to update non-existent job {JobId}", job.Id);
				return Task.FromResult(MethodResult.Failure(
					AsyncEndpointError.FromCode("JOB_NOT_FOUND", $"Job with ID {job.Id} not found")));
			}

			// Update the job in the store
			jobs[job.Id] = job;
			job.LastUpdatedAt = _dateTimeProvider.DateTimeOffsetNow;

			_logger.LogDebug("Updated job {JobId}", job.Id);
			return Task.FromResult(MethodResult.Success());
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error updating job: {JobId}", job?.Id);
			return Task.FromResult(MethodResult.Failure(
				AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error updating job: {ex.Message}", ex)));
		}
	}

	/// <summary>
	/// Atomically claims the next available job for a specific worker.
	/// </summary>
	/// <param name="workerId">The unique identifier of the worker claiming the job.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A <see cref="MethodResult{T}"/> containing the claimed job or null if no jobs available.</returns>
	public Task<MethodResult<Job>> ClaimNextJobForWorker(Guid workerId, CancellationToken cancellationToken)
	{
		try
		{
			if (cancellationToken.IsCancellationRequested)
			{
				_logger.LogDebug("Claim next job for worker operation cancelled");
				return Task.FromCanceled<MethodResult<Job>>(cancellationToken);
			}

			var now = _dateTimeProvider.UtcNow;

			// Find the next available job (oldest queued/scheduled job)
			var availableJob = jobs.Values
				.Where(job => job.WorkerId == null)
				.Where(job =>
					job.Status == JobStatus.Queued ||
					job.Status == JobStatus.Scheduled &&
					(job.RetryDelayUntil == null || job.RetryDelayUntil <= now)
				)
				.OrderBy(job => job.CreatedAt) // Claim the oldest job first
				.FirstOrDefault();

			if (availableJob != null)
			{
				availableJob.WorkerId = workerId;
				availableJob.Status = JobStatus.InProgress;
				availableJob.StartedAt = _dateTimeProvider.DateTimeOffsetNow;

				_logger.LogInformation("Claimed job {JobId} for worker {WorkerId}", availableJob.Id, workerId);
				return Task.FromResult(MethodResult<Job>.Success(availableJob));
			}

			_logger.LogDebug("No available jobs to claim for worker {WorkerId}", workerId);
			// Return successful result with null data to indicate no jobs available (not an error)
			return Task.FromResult(MethodResult<Job>.Success(default));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error claiming next job for worker {WorkerId}", workerId);
			return Task.FromResult(MethodResult<Job>.Failure(
				AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error claiming job: {ex.Message}", ex)));
		}
	}
}
