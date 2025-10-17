using AsyncEndpoints.Infrastructure;
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

	private static readonly string _queueKey = "ae:jobs:queue";
	private static readonly string _inProgressKey = "ae:jobs:inprogress";
	private static readonly string _jobStoreErrorCode = "JOB_STORE_ERROR";

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
	public RedisJobStore(ILogger<RedisJobStore> logger, string connectionString, IDateTimeProvider dateTimeProvider, IJobHashConverter jobHashConverter, ISerializer serializer, IRedisLuaScriptService redisLuaScriptService)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
		_jobHashConverter = jobHashConverter ?? throw new ArgumentNullException(nameof(jobHashConverter));
		_serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
		_redisLuaScriptService = redisLuaScriptService ?? throw new ArgumentNullException(nameof(redisLuaScriptService));
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
	public RedisJobStore(ILogger<RedisJobStore> logger, IDatabase database, IDateTimeProvider dateTimeProvider, IJobHashConverter jobHashConverter, ISerializer serializer, IRedisLuaScriptService redisLuaScriptService)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
		_jobHashConverter = jobHashConverter ?? throw new ArgumentNullException(nameof(jobHashConverter));
		_serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
		_redisLuaScriptService = redisLuaScriptService ?? throw new ArgumentNullException(nameof(redisLuaScriptService));
		_database = database ?? throw new ArgumentNullException(nameof(database));
	}

	/// <inheritdoc />
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

			// Use hash exists check to avoid overwriting
			var jobExists = await _database.KeyExistsAsync(jobKey);
			if (jobExists)
			{
				_logger.LogError("Job with ID {JobId} already exists", job.Id);
				return MethodResult.Failure(
					AsyncEndpointError.FromCode("JOB_CREATE_FAILED", $"Job with ID {job.Id} already exists"));
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
			return MethodResult.Success();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error creating job: {JobName}", job?.Name);
			return MethodResult.Failure(
				AsyncEndpointError.FromCode(_jobStoreErrorCode, $"Unexpected error creating job: {ex.Message}", ex));
		}
	}

	/// <inheritdoc />
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

			// Get all hash fields
			var hashEntries = await _database.HashGetAllAsync(jobKey);

			if (hashEntries.Length == 0)
			{
				_logger.LogWarning("Job not found with Id {JobId} from store", id);
				return MethodResult<Job>.Failure(
					AsyncEndpointError.FromCode("JOB_NOT_FOUND", $"Job with ID {id} not found"));
			}

			var job = _jobHashConverter.ConvertFromHashEntries(hashEntries);
			if (job == null)
			{
				_logger.LogError("Conversion failed for job with ID {JobId}", id);
				return MethodResult<Job>.Failure(
					AsyncEndpointError.FromCode("DESERIALIZATION_ERROR", $"Failed to convert hash to job with ID {id}"));
			}

			return MethodResult<Job>.Success(job);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error retrieving job: {JobId}", id);
			return MethodResult<Job>.Failure(
				AsyncEndpointError.FromCode(_jobStoreErrorCode, $"Unexpected error retrieving job: {ex.Message}", ex));
		}
	}

	/// <inheritdoc />
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

			// Convert the job to hash entries and update the hash
			var hashEntries = _jobHashConverter.ConvertToHashEntries(job);
			await _database.HashSetAsync(jobKey, hashEntries);

			// Update queue and in-progress sets based on job status
			await _database.SortedSetRemoveAsync(_queueKey, job.Id.ToString());
			await _database.SortedSetRemoveAsync(_inProgressKey, job.Id.ToString());

			// Only add back to queue if it's queued or scheduled for retry
			if (job.Status == JobStatus.Queued ||
				job.Status == JobStatus.Scheduled && (job.RetryDelayUntil == null || job.RetryDelayUntil <= _dateTimeProvider.UtcNow))
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
			return MethodResult.Success();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error updating job: {JobId}", job?.Id);
			return MethodResult.Failure(
				AsyncEndpointError.FromCode(_jobStoreErrorCode, $"Unexpected error updating job: {ex.Message}", ex));
		}
	}

	/// <inheritdoc />
	public async Task<MethodResult<Job>> ClaimNextJobForWorker(Guid workerId, CancellationToken cancellationToken)
	{
		try
		{
			if (cancellationToken.IsCancellationRequested)
			{
				_logger.LogDebug("Claim next job for worker operation cancelled");
				return await Task.FromCanceled<MethodResult<Job>>(cancellationToken);
			}

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
				return MethodResult<Job>.Success(default);
			}

			var jobIdString = availableJobIds[0];
			if (!Guid.TryParse(jobIdString, out var jobId))
			{
				_logger.LogDebug("Failed to parse jobId from jobIdString {JobIdString} for worker {WorkerId}", jobIdString, workerId);
				return MethodResult<Job>.Success(default);
			}

			var result = await ClaimSingleJob(jobId, workerId);
			if (!result.IsSuccess)
			{
				_logger.LogDebug("Failed to claim job for worker {WorkerId}", workerId);
				return MethodResult<Job>.Success(default);
			}

			_logger.LogInformation("Claimed job {JobId} for worker {WorkerId}", jobId, workerId);
			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error claiming next job for worker {WorkerId}", workerId);
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
				RetryDelayUntil = string.IsNullOrEmpty(resultArray[11].ToString()) ? null : DateTime.Parse(resultArray[11].ToString()),
				WorkerId = workerId, // Newly assigned
				CreatedAt = DateTimeOffset.Parse(resultArray[13].ToString()),
				StartedAt = DateTimeOffset.Parse(resultArray[14].ToString()), // Newly set
				CompletedAt = string.IsNullOrEmpty(resultArray[15].ToString()) ? null : DateTimeOffset.Parse(resultArray[15].ToString()),
				LastUpdatedAt = DateTimeOffset.Parse(resultArray[16].ToString()) // Newly set
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
