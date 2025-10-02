using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.Infrastructure.Serialization;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AsyncEndpoints.Redis.Storage;

/// <summary>
/// A Redis-based implementation of the IJobStore interface.
/// This implementation stores jobs in Redis and is suitable for distributed deployments.
/// </summary>
public class RedisJobStore : IJobStore
{
	private readonly ILogger<RedisJobStore> _logger;
	private readonly IDatabase _database;
	private readonly IDateTimeProvider _dateTimeProvider;
	private readonly ISerializer _serializer;
	private readonly string? _connectionString;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisJobStore"/> class.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	/// <param name="connectionString">The Redis connection string.</param>
	/// <param name="dateTimeProvider">Provider for current date and time.</param>
	/// <param name="serializer">The serializer service.</param>
	public RedisJobStore(ILogger<RedisJobStore> logger, string connectionString, IDateTimeProvider dateTimeProvider, ISerializer serializer)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
		_serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
		_connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

		try
		{
			var redis = ConnectionMultiplexer.Connect(_connectionString);
			_database = redis.GetDatabase();

			// Register for connection events to handle reconnection
			redis.ConnectionFailed += (sender, e) =>
				_logger.LogError(e.Exception, "Redis connection failed: {ErrorMessage}", e.Exception?.Message);
			redis.ConnectionRestored += (sender, e) =>
				_logger.LogInformation("Redis connection restored");
		}
		catch (Exception ex)
		{
			_logger.LogCritical(ex, "Failed to connect to Redis with connection string: {ConnectionString}", _connectionString);
			throw;
		}
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisJobStore"/> class with a pre-configured database.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	/// <param name="database">The Redis database instance.</param>
	/// <param name="dateTimeProvider">Provider for current date and time.</param>
	/// <param name="serializer">The serializer service.</param>
	public RedisJobStore(ILogger<RedisJobStore> logger, IDatabase database, IDateTimeProvider dateTimeProvider, ISerializer serializer)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
		_serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
		_database = database ?? throw new ArgumentNullException(nameof(database));
	}

	/// <summary>
	/// Creates a new job in the store.
	/// </summary>
	/// <param name="job">The job to create.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A <see cref="MethodResult"/> indicating the result of the operation.</returns>
	public async Task<MethodResult> CreateJob(Job job, CancellationToken cancellationToken)
	{
		try
		{
			if (job == null)
			{
				_logger.LogWarning("Attempted to create null job");
				return MethodResult.Failure(
					AsyncEndpointError.FromCode("INVALID_JOB", "Job cannot be null"));
			}

			if (job.Id == Guid.Empty)
			{
				_logger.LogWarning("Attempted to create job with empty ID");
				return MethodResult.Failure(
					AsyncEndpointError.FromCode("INVALID_JOB_ID", "Job ID cannot be empty"));
			}

			if (cancellationToken.IsCancellationRequested)
			{
				_logger.LogDebug("Job create operation cancelled");
				return await Task.FromCanceled<MethodResult>(cancellationToken);
			}

			var jobKey = GetJobKey(job.Id);

			// Check if job already exists to avoid overwriting
			var existingJob = await _database.StringGetAsync(jobKey);
			if (!existingJob.IsNull)
			{
				_logger.LogError("Job with ID {JobId} already exists", job.Id);
				return MethodResult.Failure(
					AsyncEndpointError.FromCode("JOB_CREATE_FAILED", $"Job with ID {job.Id} already exists"));
			}

			var jobJson = _serializer.Serialize(job);
			var created = await _database.StringSetAsync(jobKey, jobJson, when: When.NotExists);

			if (!created)
			{
				_logger.LogError("Failed to create job with ID {JobId}", job.Id);
				return MethodResult.Failure(
					AsyncEndpointError.FromCode("JOB_CREATE_FAILED", $"Failed to create job with ID {job.Id}"));
			}

			// Add job to the queue set if it's queued
			if (job.Status == JobStatus.Queued)
			{
				var queueKey = GetQueueKey();
				await _database.SortedSetAddAsync(queueKey, job.Id.ToString(), GetJobScore(job));
			}

			_logger.LogInformation("Created job {JobId} with name {JobName}", job.Id, job.Name);
			return MethodResult.Success();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error creating job: {JobName}", job?.Name);
			return MethodResult.Failure(
				AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error creating job: {ex.Message}", ex));
		}
	}

	/// <summary>
	/// Retrieves a job by its unique identifier.
	/// </summary>
	/// <param name="id">The unique identifier of the job to retrieve.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A <see cref="MethodResult{T}"/> containing the job if found, or an error if not found.</returns>
	public async Task<MethodResult<Job>> GetJobById(Guid id, CancellationToken cancellationToken)
	{
		try
		{
			if (id == Guid.Empty)
			{
				_logger.LogWarning("Attempted to retrieve job with empty ID");
				return MethodResult<Job>.Failure(
					AsyncEndpointError.FromCode("INVALID_JOB_ID", "Job ID cannot be empty"));
			}

			if (cancellationToken.IsCancellationRequested)
			{
				_logger.LogDebug("Job get operation cancelled for ID {JobId}", id);
				return await Task.FromCanceled<MethodResult<Job>>(cancellationToken);
			}

			var jobKey = GetJobKey(id);
			var jobJson = await _database.StringGetAsync(jobKey);

			if (jobJson.IsNull)
			{
				_logger.LogWarning("Job not found with Id {JobId} from store", id);
				return MethodResult<Job>.Failure(
					AsyncEndpointError.FromCode("JOB_NOT_FOUND", $"Job with ID {id} not found"));
			}

			var job = _serializer.Deserialize<Job>(jobJson.ToString());
			if (job == null)
			{
				_logger.LogError("Deserialization failed for job with ID {JobId}", id);
				return MethodResult<Job>.Failure(
					AsyncEndpointError.FromCode("DESERIALIZATION_ERROR", $"Failed to deserialize job with ID {id}"));
			}

			return MethodResult<Job>.Success(job);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error retrieving job: {JobId}", id);
			return MethodResult<Job>.Failure(
				AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error retrieving job: {ex.Message}", ex));
		}
	}

	/// <summary>
	/// Updates the complete job entity.
	/// </summary>
	/// <param name="job">The updated job entity.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A <see cref="MethodResult"/> indicating the result of the operation.</returns>
	public async Task<MethodResult> UpdateJob(Job job, CancellationToken cancellationToken)
	{
		try
		{
			if (job == null)
			{
				_logger.LogWarning("Attempted to update null job");
				return MethodResult.Failure(
					AsyncEndpointError.FromCode("INVALID_JOB", "Job cannot be null"));
			}

			if (job.Id == Guid.Empty)
			{
				_logger.LogWarning("Attempted to update job with empty ID");
				return MethodResult.Failure(
					AsyncEndpointError.FromCode("INVALID_JOB_ID", "Job ID cannot be empty"));
			}

			if (cancellationToken.IsCancellationRequested)
			{
				_logger.LogDebug("Job update operation cancelled for ID {JobId}", job.Id);
				return await Task.FromCanceled<MethodResult>(cancellationToken);
			}

			var jobKey = GetJobKey(job.Id);
			var jobExists = await _database.KeyExistsAsync(jobKey);

			if (!jobExists)
			{
				_logger.LogWarning("Attempted to update non-existent job {JobId}", job.Id);
				return MethodResult.Failure(
					AsyncEndpointError.FromCode("JOB_NOT_FOUND", $"Job with ID {job.Id} not found"));
			}

			// Update the last updated timestamp
			job.LastUpdatedAt = _dateTimeProvider.DateTimeOffsetNow;
			var jobJson = _serializer.Serialize(job);

			var updated = await _database.StringSetAsync(jobKey, jobJson);
			if (!updated)
			{
				_logger.LogError("Failed to update job with ID {JobId}", job.Id);
				return MethodResult.Failure(
					AsyncEndpointError.FromCode("JOB_UPDATE_FAILED", $"Failed to update job with ID {job.Id}"));
			}

			// Update queue if job status has changed
			var queueKey = GetQueueKey();
			await _database.SortedSetRemoveAsync(queueKey, job.Id.ToString());

			// Only add back to queue if it's queued or scheduled for retry
			if (job.Status == JobStatus.Queued ||
				job.Status == JobStatus.Scheduled && (job.RetryDelayUntil == null || job.RetryDelayUntil <= _dateTimeProvider.UtcNow))
			{
				await _database.SortedSetAddAsync(queueKey, job.Id.ToString(), GetJobScore(job));
			}

			_logger.LogDebug("Updated job {JobId}", job.Id);
			return MethodResult.Success();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error updating job: {JobId}", job?.Id);
			return MethodResult.Failure(
				AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error updating job: {ex.Message}", ex));
		}
	}

	/// <summary>
	/// Atomically claims available jobs for a specific worker.
	/// </summary>
	/// <param name="workerId">The unique identifier of the worker claiming the jobs.</param>
	/// <param name="maxClaimCount">The maximum number of jobs to claim.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
	/// <returns>A <see cref="MethodResult{T}"/> containing the list of claimed jobs.</returns>
	public async Task<MethodResult<List<Job>>> ClaimJobsForWorker(Guid workerId, int maxClaimCount, CancellationToken cancellationToken)
	{
		try
		{
			if (cancellationToken.IsCancellationRequested)
			{
				_logger.LogDebug("Claim jobs for worker operation cancelled");
				return await Task.FromCanceled<MethodResult<List<Job>>>(cancellationToken);
			}

			var queueKey = GetQueueKey();

			// Get available jobs from the queue, considering retry delays
			var availableJobIds = await _database.SortedSetRangeByScoreAsync(
				queueKey,
				start: double.NegativeInfinity,
				stop: GetScoreForTime(_dateTimeProvider.UtcNow),
				exclude: Exclude.None,
				skip: 0,
				take: maxClaimCount
			);

			var claimedJobs = new List<Job>();

			foreach (var jobIdString in availableJobIds)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					break;
				}

				if (Guid.TryParse(jobIdString, out var jobId))
				{
					var result = await ClaimSingleJob(jobId, workerId, cancellationToken);
					if (result.IsSuccess)
					{
						claimedJobs.Add(result.Data);
					}
				}
			}

			_logger.LogInformation("Claimed {Count} jobs for worker {WorkerId}", claimedJobs.Count, workerId);
			return MethodResult<List<Job>>.Success(claimedJobs);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error claiming jobs for worker {WorkerId}", workerId);
			return MethodResult<List<Job>>.Failure(
				AsyncEndpointError.FromCode("JOB_STORE_ERROR", $"Unexpected error claiming jobs: {ex.Message}", ex));
		}
	}

	private async Task<MethodResult<Job>> ClaimSingleJob(Guid jobId, Guid workerId, CancellationToken cancellationToken)
	{
		var jobKey = GetJobKey(jobId);

		// Get current job
		var jobJson = await _database.StringGetAsync(jobKey);
		if (jobJson.IsNull)
		{
			return MethodResult<Job>.Failure(AsyncEndpointError.FromCode("JOB_NOT_FOUND", "Job not found"));
		}

		var job = _serializer.Deserialize<Job>(jobJson.ToString());
		if (job == null)
		{
			return MethodResult<Job>.Failure(AsyncEndpointError.FromCode("DESERIALIZATION_ERROR", "Failed to deserialize job"));
		}

		// Check if job can be claimed
		var now = _dateTimeProvider.UtcNow;
		if (job.WorkerId != null ||
			job.Status != JobStatus.Queued && job.Status != JobStatus.Scheduled ||
			job.RetryDelayUntil != null && job.RetryDelayUntil > now)
		{
			// Job cannot be claimed
			return MethodResult<Job>.Failure(AsyncEndpointError.FromCode("JOB_NOT_CLAIMED", "Could not claim job"));
		}

		// Update job properties
		var updatedJob = new Job
		{
			// Copy all properties from the original job
			Id = job.Id,
			Name = job.Name,
			Status = JobStatus.InProgress,
			Headers = job.Headers,
			RouteParams = job.RouteParams,
			QueryParams = job.QueryParams,
			Payload = job.Payload,
			Result = job.Result,
			Error = job.Error,
			RetryCount = job.RetryCount,
			MaxRetries = job.MaxRetries,
			RetryDelayUntil = job.RetryDelayUntil,
			WorkerId = workerId, // Set the worker ID
			CreatedAt = job.CreatedAt,
			StartedAt = _dateTimeProvider.DateTimeOffsetNow, // Set started time
			CompletedAt = job.CompletedAt,
			LastUpdatedAt = _dateTimeProvider.DateTimeOffsetNow, // Update last updated time
		};

		try
		{
			// Use optimistic locking with a Lua script
			var luaScript = @"
                local jobKey = ARGV[1]
                local expectedValue = ARGV[2]
                local newValue = ARGV[3]
                local queueKey = ARGV[4]
                local jobId = ARGV[5]
                
                local currentValue = redis.call('GET', jobKey)
                
                if currentValue == expectedValue then
                    redis.call('SET', jobKey, newValue)
                    redis.call('ZREM', queueKey, jobId)
                    return 1
                else
                    return 0
                end
            ";

			var updatedJobJson = _serializer.Serialize(updatedJob);
			var result = await _database.ScriptEvaluateAsync(
				luaScript,
				values:
				[
					jobKey,
					jobJson,  // Expected current value 
                    updatedJobJson,  // New value
                    GetQueueKey(),
					jobId.ToString()
				]
			);

			if ((int)result == 1)
			{
				// Successfully claimed the job
				return MethodResult<Job>.Success(updatedJob);
			}
			else
			{
				// The job was updated by another worker between the get and update, so we couldn't claim it
				return MethodResult<Job>.Failure(AsyncEndpointError.FromCode("JOB_NOT_CLAIMED", "Could not claim job"));
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error claiming single job {JobId} for worker {WorkerId}", jobId, workerId);
			return MethodResult<Job>.Failure(AsyncEndpointError.FromCode("JOB_CLAIM_ERROR", $"Error claiming job: {ex.Message}", ex));
		}
	}

	private static string GetJobKey(Guid jobId) => $"ae:job:{jobId}";

	private static string GetQueueKey() => "ae:jobs:queue";

	private static double GetJobScore(Job job)
	{
		// Use timestamp as score for the sorted set to prioritize older jobs
		// If there's a retry delay, use that time instead
		var effectiveTime = job.RetryDelayUntil ?? job.CreatedAt.UtcDateTime;
		return GetScoreForTime(effectiveTime);
	}

	private static double GetScoreForTime(DateTime dateTime)
	{
		// Convert to Unix timestamp for use as score in sorted set
		return (dateTime - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
	}
}