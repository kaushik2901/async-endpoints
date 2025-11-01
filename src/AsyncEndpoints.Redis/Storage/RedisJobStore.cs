using System.Diagnostics;
using System.Globalization;
using AsyncEndpoints.Infrastructure;
using AsyncEndpoints.Infrastructure.Observability;
using AsyncEndpoints.Infrastructure.Serialization;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.Redis.Services;
using AsyncEndpoints.Utilities;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AsyncEndpoints.Redis.Storage;

/// <inheritdoc />
/// <summary>
/// A Redis-based implementation of IJobStore that provides distributed job storage suitable for multi-instance deployments.
/// This implementation uses Redis for persistence and supports job recovery operations.
/// </summary>
public class RedisJobStore : IJobStore
{
	private readonly ILogger<RedisJobStore> _logger;
	private readonly IDatabase _database;
	private readonly IDateTimeProvider _dateTimeProvider;
	private readonly IJobHashConverter _jobHashConverter;
	private readonly ISerializer _serializer;
	private readonly IRedisLuaScriptService _redisLuaScriptService;
	private readonly IAsyncEndpointsObservability _metrics;

	private static readonly string _queueKey = "ae:jobs:queue";
	private static readonly string _inProgressKey = "ae:jobs:inprogress";
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
	private static readonly string _deserializationError = "DESERIALIZATION_ERROR";
	private static readonly string _errorTypeTag = "error.type";
	private static readonly string _parseError = "PARSE_ERROR";

	public bool SupportsJobRecovery => true; // Redis supports recovery

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisJobStore"/> class.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	/// <param name="connectionString">The Redis connection string.</param>
	/// <param name="dateTimeProvider">Provider for current date and time.</param>
	/// <param name="jobHashConverter">The job hash converter service.</param>
	/// <param name="serializer">The serializer service.</param>
	/// <param name="redisLuaScriptService">Service for executing Redis Lua scripts.</param>
	/// <param name="metrics">The observability metrics service.</param>
	public RedisJobStore(ILogger<RedisJobStore> logger, string connectionString, IDateTimeProvider dateTimeProvider, IJobHashConverter jobHashConverter, ISerializer serializer, IRedisLuaScriptService redisLuaScriptService, IAsyncEndpointsObservability metrics)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
		_jobHashConverter = jobHashConverter ?? throw new ArgumentNullException(nameof(jobHashConverter));
		_serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
		_redisLuaScriptService = redisLuaScriptService ?? throw new ArgumentNullException(nameof(redisLuaScriptService));
		_metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
		_database = InitializeDatabase(connectionString ?? throw new ArgumentNullException(nameof(connectionString)));
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisJobStore"/> class with a pre-configured database.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	/// <param name="database">The Redis database instance.</param>
	/// <param name="dateTimeProvider">Provider for current date and time.</param>
	/// <param name="jobHashConverter">The job hash converter service.</param>
	/// <param name="serializer">The serializer service.</param>
	/// <param name="redisLuaScriptService">Service for executing Redis Lua scripts.</param>
	/// <param name="metrics">The observability metrics service.</param>
	public RedisJobStore(ILogger<RedisJobStore> logger, IDatabase database, IDateTimeProvider dateTimeProvider, IJobHashConverter jobHashConverter, ISerializer serializer, IRedisLuaScriptService redisLuaScriptService, IAsyncEndpointsObservability metrics)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
		_jobHashConverter = jobHashConverter ?? throw new ArgumentNullException(nameof(jobHashConverter));
		_serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
		_redisLuaScriptService = redisLuaScriptService ?? throw new ArgumentNullException(nameof(redisLuaScriptService));
		_metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
		_database = database ?? throw new ArgumentNullException(nameof(database));
	}

	/// <inheritdoc />
	public async Task<MethodResult> CreateJob(Job job, CancellationToken cancellationToken)
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

				return MethodResult.Failure(
					AsyncEndpointError.FromCode(_invalidJobErrorCode, "Job cannot be null"));
			}

			if (job.Id == Guid.Empty)
			{
				_logger.LogWarning("Attempted to create job with empty ID");
				_metrics.RecordStoreError(_createJobOperationName, _invalidJobIdErrorCode, this.GetType().Name);
				activity?.SetStatus(ActivityStatusCode.Error, "Invalid job ID");
				activity?.SetTag(_errorTypeTag, _invalidJobIdErrorCode);

				return MethodResult.Failure(
					AsyncEndpointError.FromCode(_invalidJobIdErrorCode, "Job ID cannot be empty"));
			}

			if (cancellationToken.IsCancellationRequested)
			{
				_logger.LogDebug("Job create operation cancelled");
				return await Task.FromCanceled<MethodResult>(cancellationToken);
			}

			var jobKey = GetJobKey(job.Id);

			// Use hash exists check to avoid overwriting
			var jobExists = await _database.KeyExistsAsync(jobKey);
			if (jobExists)
			{
				_logger.LogError("Job with ID {JobId} already exists", job.Id);
				_metrics.RecordStoreError(_createJobOperationName, _duplicateJobErrorCode, this.GetType().Name);
				activity?.SetStatus(ActivityStatusCode.Error, "Duplicate job");
				activity?.SetTag(_errorTypeTag, _duplicateJobErrorCode);

				return MethodResult.Failure(
					AsyncEndpointError.FromCode(_jobCreateFailedErrorCode, $"Job with ID {job.Id} already exists"));
			}

			// Create job in Redis as a hash
			var hashEntries = _jobHashConverter.ConvertToHashEntries(job);
			await _database.HashSetAsync(jobKey, hashEntries);

			// Add job to the queue set if it's queued
			if (job.Status == JobStatus.Queued)
			{
				await _database.SortedSetAddAsync(_queueKey, job.Id.ToString(), GetJobScore(job));
			}

			_logger.LogInformation("Created job {JobId} with name {JobName}", job.Id, job.Name);
			var duration = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
			_metrics.RecordStoreOperationDuration(_createJobOperationName, this.GetType().Name, duration);
			_metrics.RecordStoreOperation(_createJobOperationName, this.GetType().Name);

			return MethodResult.Success();
		}
		catch (Exception ex)
		{
			_metrics.RecordStoreError(_createJobOperationName, ex.GetType().Name, this.GetType().Name);
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			activity?.SetTag("error.type", ex.GetType().Name);

			_logger.LogError(ex, "Unexpected error creating job: {JobName}", job?.Name);
			var duration = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
			_metrics.RecordStoreOperationDuration(_createJobOperationName, this.GetType().Name, duration);

			return MethodResult.Failure(
				AsyncEndpointError.FromCode(_jobStoreErrorCode, $"Unexpected error creating job: {ex.Message}", ex));
		}
	}

	/// <inheritdoc />
	public async Task<MethodResult<Job>> GetJobById(Guid id, CancellationToken cancellationToken)
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

				return MethodResult<Job>.Failure(
					AsyncEndpointError.FromCode(_invalidJobIdErrorCode, "Job ID cannot be empty"));
			}

			if (cancellationToken.IsCancellationRequested)
			{
				_logger.LogDebug("Job get operation cancelled for ID {JobId}", id);
				return await Task.FromCanceled<MethodResult<Job>>(cancellationToken);
			}

			var jobKey = GetJobKey(id);

			// Get all hash fields
			var hashEntries = await _database.HashGetAllAsync(jobKey);

			if (hashEntries.Length == 0)
			{
				_logger.LogWarning("Job not found with Id {JobId} from store", id);
				_metrics.RecordStoreError(_getJobByIdOperationName, _jobNotFoundErrorCode, this.GetType().Name);
				activity?.SetStatus(ActivityStatusCode.Error, "Job not found");
				activity?.SetTag(_errorTypeTag, _jobNotFoundErrorCode);

				return MethodResult<Job>.Failure(
					AsyncEndpointError.FromCode(_jobNotFoundErrorCode, $"Job with ID {id} not found"));
			}

			var job = _jobHashConverter.ConvertFromHashEntries(hashEntries);
			if (job == null)
			{
				_logger.LogError("Conversion failed for job with ID {JobId}", id);
				_metrics.RecordStoreError(_getJobByIdOperationName, _deserializationError, this.GetType().Name);
				activity?.SetStatus(ActivityStatusCode.Error, "Deserialization error");
				activity?.SetTag(_errorTypeTag, _deserializationError);

				return MethodResult<Job>.Failure(
					AsyncEndpointError.FromCode(_deserializationError, $"Failed to convert hash to job with ID {id}"));
			}

			var duration = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
			_metrics.RecordStoreOperationDuration(_getJobByIdOperationName, this.GetType().Name, duration);
			_metrics.RecordStoreOperation(_getJobByIdOperationName, this.GetType().Name);

			return MethodResult<Job>.Success(job);
		}
		catch (Exception ex)
		{
			_metrics.RecordStoreError(_getJobByIdOperationName, ex.GetType().Name, this.GetType().Name);
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			activity?.SetTag(_errorTypeTag, ex.GetType().Name);

			_logger.LogError(ex, "Unexpected error retrieving job: {JobId}", id);
			var duration = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
			_metrics.RecordStoreOperationDuration(_getJobByIdOperationName, this.GetType().Name, duration);

			return MethodResult<Job>.Failure(
				AsyncEndpointError.FromCode(_jobStoreErrorCode, $"Unexpected error retrieving job: {ex.Message}", ex));
		}
	}

	/// <inheritdoc />
	public async Task<MethodResult> UpdateJob(Job job, CancellationToken cancellationToken)
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

				return MethodResult.Failure(
					AsyncEndpointError.FromCode(_invalidJobErrorCode, "Job cannot be null"));
			}

			if (job.Id == Guid.Empty)
			{
				_logger.LogWarning("Attempted to update job with empty ID");
				_metrics.RecordStoreError(_updateJobOperationName, _invalidJobIdErrorCode, this.GetType().Name);
				activity?.SetStatus(ActivityStatusCode.Error, "Invalid job ID");
				activity?.SetTag(_errorTypeTag, _invalidJobIdErrorCode);

				return MethodResult.Failure(
					AsyncEndpointError.FromCode(_invalidJobIdErrorCode, "Job ID cannot be empty"));
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
				_metrics.RecordStoreError(_updateJobOperationName, _jobNotFoundErrorCode, this.GetType().Name);
				activity?.SetStatus(ActivityStatusCode.Error, "Job not found");
				activity?.SetTag(_errorTypeTag, _jobNotFoundErrorCode);

				return MethodResult.Failure(
					AsyncEndpointError.FromCode(_jobNotFoundErrorCode, $"Job with ID {job.Id} not found"));
			}

			// Update the last updated timestamp
			job.LastUpdatedAt = _dateTimeProvider.DateTimeOffsetNow;

			// Convert the job to hash entries and update the hash
			var hashEntries = _jobHashConverter.ConvertToHashEntries(job);
			await _database.HashSetAsync(jobKey, hashEntries);

			// Update queue and in-progress sets based on job status
			await _database.SortedSetRemoveAsync(_queueKey, job.Id.ToString());
			await _database.SortedSetRemoveAsync(_inProgressKey, job.Id.ToString());

			// Only add back to queue if it's queued or scheduled for retry
			if (job.Status == JobStatus.Queued || job.Status == JobStatus.Scheduled)
			{
				await _database.SortedSetAddAsync(_queueKey, job.Id.ToString(), GetJobScore(job));
			}
			// Add to in-progress set if status is InProgress and has a worker assigned
			else if (job.Status == JobStatus.InProgress && job.WorkerId.HasValue)
			{
				// Add to in-progress set with started timestamp as score for efficient recovery scanning
				var startedAtScore = (job.StartedAt?.ToUnixTimeSeconds() ?? _dateTimeProvider.DateTimeOffsetNow.ToUnixTimeSeconds()).ToString();
				await _database.SortedSetAddAsync(_inProgressKey, job.Id.ToString(), double.Parse(startedAtScore));
			}

			_logger.LogDebug("Updated job {JobId}", job.Id);
			var duration = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
			_metrics.RecordStoreOperationDuration(_updateJobOperationName, this.GetType().Name, duration);
			_metrics.RecordStoreOperation(_updateJobOperationName, this.GetType().Name);

			return MethodResult.Success();
		}
		catch (Exception ex)
		{
			_metrics.RecordStoreError(_updateJobOperationName, ex.GetType().Name, this.GetType().Name);
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			activity?.SetTag(_errorTypeTag, ex.GetType().Name);

			_logger.LogError(ex, "Unexpected error updating job: {JobId}", job?.Id);
			var duration = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
			_metrics.RecordStoreOperationDuration(_updateJobOperationName, this.GetType().Name, duration);

			return MethodResult.Failure(
				AsyncEndpointError.FromCode(_jobStoreErrorCode, $"Unexpected error updating job: {ex.Message}", ex));
		}
	}

	/// <inheritdoc />
	public async Task<MethodResult<Job>> ClaimNextJobForWorker(Guid workerId, CancellationToken cancellationToken)
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
				return await Task.FromCanceled<MethodResult<Job>>(cancellationToken);
			}

			_logger.LogDebug("Attempting to claim next job for worker {WorkerId}", workerId);

			// Get the next available job from the queue (oldest first), considering retry delays
			var availableJobIds = await _database.SortedSetRangeByScoreAsync(
				_queueKey,
				start: double.NegativeInfinity,
				stop: GetScoreForTime(_dateTimeProvider.UtcNow),
				exclude: Exclude.None,
				skip: 0,
				take: 1  // Only take the next available job
			);

			if (availableJobIds.Length == 0)
			{
				_logger.LogDebug("No available jobs to claim for worker {WorkerId}", workerId);
				var noJobsDuration = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
				_metrics.RecordStoreOperationDuration(_claimNextJobOperationName, this.GetType().Name, noJobsDuration);
				_metrics.RecordStoreOperation(_claimNextJobOperationName, this.GetType().Name);

				return MethodResult<Job>.Success(default);
			}

			var jobIdString = availableJobIds[0];
			if (!Guid.TryParse(jobIdString, out var jobId))
			{
				_logger.LogDebug("Failed to parse jobId from jobIdString {JobIdString} for worker {WorkerId}", jobIdString, workerId);
				var parseErrorDuration = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
				_metrics.RecordStoreOperationDuration(_claimNextJobOperationName, this.GetType().Name, parseErrorDuration);
				_metrics.RecordStoreError(_claimNextJobOperationName, _parseError, this.GetType().Name);
				activity?.SetStatus(ActivityStatusCode.Error, "Parse error");
				activity?.SetTag(_errorTypeTag, _parseError);

				return MethodResult<Job>.Success(default);
			}

			_logger.LogDebug("Attempting to claim job {JobId} for worker {WorkerId}", jobId, workerId);
			var result = await ClaimSingleJob(jobId, workerId);
			if (!result.IsSuccess)
			{
				_logger.LogDebug("Failed to claim job for worker {WorkerId}", workerId);
				_metrics.RecordStoreError(_claimNextJobOperationName, result.Error.Code, this.GetType().Name);
				activity?.SetStatus(ActivityStatusCode.Error, result.Error.Message);
				activity?.SetTag(_errorTypeTag, result.Error.Code);

				var claimFailureDuration = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
				_metrics.RecordStoreOperationDuration(_claimNextJobOperationName, this.GetType().Name, claimFailureDuration);

				return MethodResult<Job>.Success(default);
			}

			_logger.LogInformation("Successfully claimed job {JobId} for worker {WorkerId}", jobId, workerId);
			activity?.SetTag("job.id", jobId.ToString());
			var successDuration = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
			_metrics.RecordStoreOperationDuration(_claimNextJobOperationName, this.GetType().Name, successDuration);
			_metrics.RecordStoreOperation(_claimNextJobOperationName, this.GetType().Name);

			return result;
		}
		catch (Exception ex)
		{
			_metrics.RecordStoreError(_claimNextJobOperationName, ex.GetType().Name, this.GetType().Name);
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			activity?.SetTag(_errorTypeTag, ex.GetType().Name);

			_logger.LogError(ex, "Unexpected error claiming next job for worker {WorkerId}", workerId);
			var duration = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
			_metrics.RecordStoreOperationDuration(_claimNextJobOperationName, this.GetType().Name, duration);

			return MethodResult<Job>.Failure(
				AsyncEndpointError.FromCode(_jobStoreErrorCode, $"Unexpected error claiming job: {ex.Message}", ex));
		}
	}

	private async Task<MethodResult<Job>> ClaimSingleJob(Guid jobId, Guid workerId)
	{
		var result = await _redisLuaScriptService.ClaimSingleJob(_database, jobId, workerId);

		if (!result.IsSuccess)
		{
			return MethodResult<Job>.Failure(result.Error);
		}

		try
		{
			// Construct the job from the returned Redis values
			var resultArray = result.Data;

			var claimedJob = new Job
			{
				Id = Guid.Parse(resultArray[0].ToString()),
				Name = resultArray[1].ToString(),
				Status = (JobStatus)int.Parse(resultArray[2].ToString()),
				Headers = string.IsNullOrEmpty(resultArray[3].ToString()) ? [] : Deserialize<Dictionary<string, List<string?>>>(resultArray[3].ToString()) ?? [],
				RouteParams = string.IsNullOrEmpty(resultArray[4].ToString()) ? [] : Deserialize<Dictionary<string, object?>>(resultArray[4].ToString()) ?? [],
				QueryParams = string.IsNullOrEmpty(resultArray[5].ToString()) ? [] : Deserialize<List<KeyValuePair<string, List<string?>>>>(resultArray[5].ToString()) ?? [],
				Payload = resultArray[6].ToString(),
				Result = string.IsNullOrEmpty(resultArray[7].ToString()) ? null : resultArray[7].ToString(),
				Error = string.IsNullOrEmpty(resultArray[8].ToString()) ? null : Deserialize<AsyncEndpointError>(resultArray[8].ToString()),
				RetryCount = int.Parse(resultArray[9].ToString()),
				MaxRetries = int.Parse(resultArray[10].ToString()),
				RetryDelayUntil = string.IsNullOrEmpty(resultArray[11].ToString()) ? null : DateTime.ParseExact(resultArray[11].ToString(), "O", CultureInfo.InvariantCulture),
				WorkerId = workerId, // Newly assigned
				CreatedAt = DateTimeOffset.ParseExact(resultArray[13].ToString(), "O", CultureInfo.InvariantCulture),
				StartedAt = DateTimeOffset.ParseExact(resultArray[14].ToString(), "O", CultureInfo.InvariantCulture), // Newly set
				CompletedAt = string.IsNullOrEmpty(resultArray[15].ToString()) ? null : DateTimeOffset.ParseExact(resultArray[15].ToString(), "O", CultureInfo.InvariantCulture),
				LastUpdatedAt = DateTimeOffset.ParseExact(resultArray[16].ToString(), "O", CultureInfo.InvariantCulture) // Newly set
			};

			return MethodResult<Job>.Success(claimedJob);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error constructing job object after claiming job {JobId}", jobId);
			return MethodResult<Job>.Failure(AsyncEndpointError.FromCode("JOB_CONSTRUCTION_ERROR", $"Error constructing job object: {ex.Message}"));
		}
	}

	private IDatabase InitializeDatabase(string connectionString)
	{
		var redis = ConnectionMultiplexer.Connect(connectionString);

		// Register for connection events to handle reconnection
		redis.ConnectionFailed += (sender, e) =>
			_logger.LogError(e.Exception, "Redis connection failed: {ErrorMessage}", e.Exception?.Message);
		redis.ConnectionRestored += (sender, e) =>
			_logger.LogInformation("Redis connection restored");

		return redis.GetDatabase();
	}

	private static string GetJobKey(Guid jobId) => $"ae:job:{jobId}";

	private static double GetJobScore(Job job)
	{
		// Use timestamp as score for the sorted set to prioritize older jobs
		// If there's a retry delay, use that time instead
		var effectiveTime = job.RetryDelayUntil ?? job.CreatedAt.UtcDateTime;
		return GetScoreForTime(effectiveTime);
	}

	private static double GetScoreForTime(DateTime dateTime)
	{
		return (dateTime - DateTime.UnixEpoch).TotalSeconds;
	}

	private T? Deserialize<T>(string value)
	{
		if (string.IsNullOrEmpty(value)) return default;
		return _serializer.Deserialize<T>(value);
	}

	public async Task<int> RecoverStuckJobs(long timeoutUnixTime, int maxRetries, CancellationToken cancellationToken)
	{
		return await _redisLuaScriptService.RecoverStuckJobs(_database, timeoutUnixTime, maxRetries);
	}
}
