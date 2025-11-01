using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.Infrastructure.Observability;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.Logging;

namespace AsyncEndpoints.JobProcessing;

/// <inheritdoc />
/// <summary>
/// An in-memory implementation of IJobStore that uses a thread-safe concurrent dictionary for storage.
/// This implementation is suitable for development or single-instance deployments but does not support job recovery.
/// </summary>
public class InMemoryJobStore(ILogger<InMemoryJobStore> logger, IDateTimeProvider dateTimeProvider, IAsyncEndpointsObservability metrics) : IJobStore
{
	private readonly ILogger<InMemoryJobStore> _logger = logger;
	private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
	private readonly IAsyncEndpointsObservability _metrics = metrics;
	private readonly ConcurrentDictionary<Guid, Job> jobs = new();

	private static readonly string _jobStoreErrorCode = "JOB_STORE_ERROR";
	private static readonly string _createJobOperationName = "CreateJob";
	private static readonly string _getJobByIdOperationName = "GetJobById";
	private static readonly string _updateJobOperationName = "UpdateJob";
	private static readonly string _claimNextJobOperationName = "ClaimNextJob";
	private static readonly string _invalidJobErrorCode = "INVALID_JOB";
	private static readonly string _invalidJobIdErrorCode = "INVALID_JOB_ID";
	private static readonly string _jobNotFoundErrorCode = "JOB_NOT_FOUND";
	private static readonly string _duplicateJobErrorCode = "DUPLICATE_JOB";
	private static readonly string _jobCreateFailedErrorCode = "JOB_CREATE_FAILED";
	private static readonly string _concurrencyConflict = "CONCURRENCY_CONFLICT";
	private static readonly string _errorTypeTag = "error.type";

	public bool SupportsJobRecovery => false; // In-memory store doesn't support recovery

	public Task<int> RecoverStuckJobs(long timeoutUnixTime, int maxRetries, CancellationToken cancellationToken)
	{
		throw new NotSupportedException("In-memory job store does not support job recovery operations.");
	}

	/// <inheritdoc />
	public Task<MethodResult> CreateJob(Job job, CancellationToken cancellationToken)
	{
		// Start activity only if tracing is enabled
		using var activity = _metrics.StartStoreOperationActivity(_createJobOperationName, this.GetType().Name, job?.Id);
		
		var startTime = DateTimeOffset.UtcNow;
		try
		{
			if (job == null)
			{
				_logger.LogWarning("Attempted to create null job");
				_metrics.RecordStoreError(_createJobOperationName, _invalidJobErrorCode, this.GetType().Name);
				activity?.SetStatus(ActivityStatusCode.Error, "Invalid job");
				activity?.SetTag(_errorTypeTag, _invalidJobErrorCode);
				
				return Task.FromResult(MethodResult.Failure(
					AsyncEndpointError.FromCode(_invalidJobErrorCode, "Job cannot be null")));
			}

			if (job.Id == Guid.Empty)
			{
				_logger.LogWarning("Attempted to create job with empty ID");
				_metrics.RecordStoreError(_createJobOperationName, _invalidJobIdErrorCode, this.GetType().Name);
				activity?.SetStatus(ActivityStatusCode.Error, "Invalid job ID");
				activity?.SetTag(_errorTypeTag, _invalidJobIdErrorCode);
				
				return Task.FromResult(MethodResult.Failure(
					AsyncEndpointError.FromCode(_invalidJobIdErrorCode, "Job ID cannot be empty")));
			}

			if (cancellationToken.IsCancellationRequested)
			{
				_logger.LogDebug("Job create operation cancelled");
				return Task.FromCanceled<MethodResult>(cancellationToken);
			}

			if (!jobs.TryAdd(job.Id, job))
			{
				_logger.LogError("Failed to create job with ID {JobId}", job.Id);
				_metrics.RecordStoreError(_createJobOperationName, _duplicateJobErrorCode, this.GetType().Name);
				activity?.SetStatus(ActivityStatusCode.Error, "Duplicate job");
				activity?.SetTag(_errorTypeTag, _duplicateJobErrorCode);
				
				return Task.FromResult(MethodResult.Failure(
					AsyncEndpointError.FromCode(_jobCreateFailedErrorCode, $"Failed to create job with ID {job.Id}")));
			}

			_logger.LogInformation("Created job {JobId} with name {JobName}", job.Id, job.Name);
			var duration = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
			_metrics.RecordStoreOperationDuration(_createJobOperationName, this.GetType().Name, duration);
			_metrics.RecordStoreOperation(_createJobOperationName, this.GetType().Name);
			
			return Task.FromResult(MethodResult.Success());
		}
		catch (Exception ex)
		{
			_metrics.RecordStoreError(_createJobOperationName, ex.GetType().Name, this.GetType().Name);
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			activity?.SetTag(_errorTypeTag, ex.GetType().Name);
			
			_logger.LogError(ex, "Unexpected error creating job: {JobName}", job?.Name);
			var duration = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
			_metrics.RecordStoreOperationDuration(_createJobOperationName, this.GetType().Name, duration);
			
			return Task.FromResult(MethodResult.Failure(
				AsyncEndpointError.FromCode(_jobStoreErrorCode, $"Unexpected error creating job: {ex.Message}", ex)));
		}
	}

	/// <inheritdoc />
	public Task<MethodResult<Job>> GetJobById(Guid id, CancellationToken cancellationToken)
	{
		// Start activity only if tracing is enabled
		using var activity = _metrics.StartStoreOperationActivity(_getJobByIdOperationName, this.GetType().Name, id);
		
		var startTime = DateTimeOffset.UtcNow;
		try
		{
			if (id == Guid.Empty)
			{
				_logger.LogWarning("Attempted to retrieve job with empty ID");
				_metrics.RecordStoreError(_getJobByIdOperationName, _invalidJobIdErrorCode, this.GetType().Name);
				activity?.SetStatus(ActivityStatusCode.Error, "Invalid job ID");
				activity?.SetTag(_errorTypeTag, _invalidJobIdErrorCode);
				
				return Task.FromResult(MethodResult<Job>.Failure(
					AsyncEndpointError.FromCode(_invalidJobIdErrorCode, "Job ID cannot be empty")));
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
				_metrics.RecordStoreError(_getJobByIdOperationName, _jobNotFoundErrorCode, this.GetType().Name);
				activity?.SetStatus(ActivityStatusCode.Error, "Job not found");
				activity?.SetTag(_errorTypeTag, _jobNotFoundErrorCode);
				
				return Task.FromResult(MethodResult<Job>.Failure(
					AsyncEndpointError.FromCode(_jobNotFoundErrorCode, $"Job with ID {id} not found")));
			}

			var duration = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
			_metrics.RecordStoreOperationDuration(_getJobByIdOperationName, this.GetType().Name, duration);
			_metrics.RecordStoreOperation(_getJobByIdOperationName, this.GetType().Name);
			
			return Task.FromResult(MethodResult<Job>.Success(job));
		}
		catch (Exception ex)
		{
			_metrics.RecordStoreError(_getJobByIdOperationName, ex.GetType().Name, this.GetType().Name);
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			activity?.SetTag(_errorTypeTag, ex.GetType().Name);
			
			_logger.LogError(ex, "Unexpected error retrieving job: {JobId}", id);
			var duration = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
			_metrics.RecordStoreOperationDuration(_getJobByIdOperationName, this.GetType().Name, duration);
			
			return Task.FromResult(MethodResult<Job>.Failure(
				AsyncEndpointError.FromCode(_jobStoreErrorCode, $"Unexpected error retrieving job: {ex.Message}", ex)));
		}
	}

	/// <inheritdoc />
	public Task<MethodResult> UpdateJob(Job job, CancellationToken cancellationToken)
	{
		// Start activity only if tracing is enabled
		using var activity = _metrics.StartStoreOperationActivity(_updateJobOperationName, this.GetType().Name, job?.Id);
		
		var startTime = DateTimeOffset.UtcNow;
		try
		{
			if (job == null)
			{
				_logger.LogWarning("Attempted to update null job");
				_metrics.RecordStoreError(_updateJobOperationName, _invalidJobErrorCode, this.GetType().Name);
				activity?.SetStatus(ActivityStatusCode.Error, "Invalid job");
				activity?.SetTag(_errorTypeTag, _invalidJobErrorCode);
				
				return Task.FromResult(MethodResult.Failure(
					AsyncEndpointError.FromCode(_invalidJobErrorCode, "Job cannot be null")));
			}

			if (job.Id == Guid.Empty)
			{
				_logger.LogWarning("Attempted to update job with empty ID");
				_metrics.RecordStoreError(_updateJobOperationName, _invalidJobIdErrorCode, this.GetType().Name);
				activity?.SetStatus(ActivityStatusCode.Error, "Invalid job ID");
				activity?.SetTag(_errorTypeTag, _invalidJobIdErrorCode);
				
				return Task.FromResult(MethodResult.Failure(
					AsyncEndpointError.FromCode(_invalidJobIdErrorCode, "Job ID cannot be empty")));
			}

			if (cancellationToken.IsCancellationRequested)
			{
				_logger.LogDebug("Job update operation cancelled for ID {JobId}", job.Id);
				return Task.FromCanceled<MethodResult>(cancellationToken);
			}

			// Use the immutable objects pattern to ensure atomic update
			if (!jobs.TryGetValue(job.Id, out var existingJob))
			{
				_logger.LogWarning("Attempted to update non-existent job {JobId}", job.Id);
				_metrics.RecordStoreError(_updateJobOperationName, _jobNotFoundErrorCode, this.GetType().Name);
				activity?.SetStatus(ActivityStatusCode.Error, "Job not found");
				activity?.SetTag(_errorTypeTag, _jobNotFoundErrorCode);
				
				return Task.FromResult(MethodResult.Failure(
					AsyncEndpointError.FromCode(_jobNotFoundErrorCode, $"Job with ID {job.Id} not found")));
			}

			// Create a new job instance with updated properties using the existing job as base
			var updatedJob = existingJob.CreateCopy(
				status: job.Status,
				workerId: job.WorkerId,
				startedAt: job.StartedAt,
				completedAt: job.CompletedAt,
				result: job.Result,
				error: job.Error,
				retryCount: job.RetryCount,
				retryDelayUntil: job.RetryDelayUntil,
				lastUpdatedAt: _dateTimeProvider.DateTimeOffsetNow // This will update the LastUpdatedAt field
			);

			// Perform atomic update using TryUpdate once (no loop needed with immutable pattern)
			if (!jobs.TryUpdate(job.Id, updatedJob, existingJob))
			{
				// If TryUpdate fails, it means another thread modified the job between our read and update
				_logger.LogWarning("Job {JobId} was modified by another thread during update", job.Id);
				_metrics.RecordStoreError(_updateJobOperationName, _concurrencyConflict, this.GetType().Name);
				activity?.SetStatus(ActivityStatusCode.Error, "Concurrency conflict");
				activity?.SetTag(_errorTypeTag, _concurrencyConflict);
				
				return Task.FromResult(MethodResult.Failure(
					AsyncEndpointError.FromCode("JOB_UPDATE_CONFLICT", "Job was modified by another thread")));
			}

			_logger.LogDebug("Updated job {JobId}", job.Id);
			var duration = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
			_metrics.RecordStoreOperationDuration(_updateJobOperationName, this.GetType().Name, duration);
			_metrics.RecordStoreOperation(_updateJobOperationName, this.GetType().Name);
			
			return Task.FromResult(MethodResult.Success());
		}
		catch (Exception ex)
		{
			_metrics.RecordStoreError(_updateJobOperationName, ex.GetType().Name, this.GetType().Name);
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			activity?.SetTag(_errorTypeTag, ex.GetType().Name);
			
			_logger.LogError(ex, "Unexpected error updating job: {JobId}", job?.Id);
			var duration = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
			_metrics.RecordStoreOperationDuration(_updateJobOperationName, this.GetType().Name, duration);
			
			return Task.FromResult(MethodResult.Failure(
				AsyncEndpointError.FromCode(_jobStoreErrorCode, $"Unexpected error updating job: {ex.Message}", ex)));
		}
	}

	/// <inheritdoc />
	public Task<MethodResult<Job>> ClaimNextJobForWorker(Guid workerId, CancellationToken cancellationToken)
	{
		using var _ = _logger.BeginScope(new { WorkerId = workerId });
		
		// Start activity only if tracing is enabled
		// Note: We don't know the specific job ID yet, so we'll pass null
		using var activity = _metrics.StartStoreOperationActivity(_claimNextJobOperationName, this.GetType().Name);
		
		var startTime = DateTimeOffset.UtcNow;
		try
		{
			if (cancellationToken.IsCancellationRequested)
			{
				_logger.LogDebug("Claim next job for worker operation cancelled");
				return Task.FromCanceled<MethodResult<Job>>(cancellationToken);
			}

			_logger.LogDebug("Attempting to claim next job for worker {WorkerId}", workerId);
			
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

			if (availableJob == null)
			{
				_logger.LogDebug("No available jobs to claim for worker {WorkerId}", workerId);
				// Return successful result with null data to indicate no jobs available (not an error)
				var noJobsDuration = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
				_metrics.RecordStoreOperationDuration(_claimNextJobOperationName, this.GetType().Name, noJobsDuration);
				_metrics.RecordStoreOperation(_claimNextJobOperationName, this.GetType().Name);
				
				return Task.FromResult(MethodResult<Job>.Success(default));
			}

			_logger.LogDebug("Found available job {JobId} for worker {WorkerId}, attempting to claim", availableJob.Id, workerId);

			// Use the immutable objects pattern to ensure atomic update of the job
			Job? currentJob;
			Job updatedJob;
			do
			{
				if (!jobs.TryGetValue(availableJob.Id, out currentJob))
				{
					_logger.LogDebug("Job {JobId} no longer exists", availableJob.Id);
					return Task.FromResult(MethodResult<Job>.Success(default));
				}

				// Check if the job was already claimed by another worker
				if (currentJob.WorkerId != null)
				{
					_logger.LogDebug("Job {JobId} was already claimed by another worker", availableJob.Id);
					return Task.FromResult(MethodResult<Job>.Success(default));
				}

				// Create a copy of the job with updated properties using the CreateCopy method
				updatedJob = currentJob.CreateCopy(
					status: JobStatus.InProgress,
					workerId: workerId,
					startedAt: _dateTimeProvider.DateTimeOffsetNow,
					lastUpdatedAt: _dateTimeProvider.DateTimeOffsetNow
				);
			} while (!jobs.TryUpdate(availableJob.Id, updatedJob, currentJob));

			_logger.LogInformation("Successfully claimed job {JobId} for worker {WorkerId}", availableJob.Id, workerId);
			activity?.SetTag("job.id", availableJob.Id.ToString());
			var successDuration = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
			_metrics.RecordStoreOperationDuration(_claimNextJobOperationName, this.GetType().Name, successDuration);
			_metrics.RecordStoreOperation(_claimNextJobOperationName, this.GetType().Name);
			
			return Task.FromResult(MethodResult<Job>.Success(updatedJob));
		}
		catch (Exception ex)
		{
			_metrics.RecordStoreError(_claimNextJobOperationName, ex.GetType().Name, this.GetType().Name);
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			activity?.SetTag(_errorTypeTag, ex.GetType().Name);
			
			_logger.LogError(ex, "Unexpected error claiming next job for worker {WorkerId}", workerId);
			var errorDuration = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
			_metrics.RecordStoreOperationDuration(_claimNextJobOperationName, this.GetType().Name, errorDuration);
			
			return Task.FromResult(MethodResult<Job>.Failure(
				AsyncEndpointError.FromCode(_jobStoreErrorCode, $"Unexpected error claiming job: {ex.Message}", ex)));
		}
	}
}
