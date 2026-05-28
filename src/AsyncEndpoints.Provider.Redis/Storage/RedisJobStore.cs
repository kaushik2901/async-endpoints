using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Abstractions.Storage;
using AsyncEndpoints.Provider.Redis.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AsyncEndpoints.Provider.Redis.Storage;

public class RedisJobStore : IJobStore
{
	private readonly ILogger<RedisJobStore> _logger;
	private readonly IDatabase _database;
	private readonly TimeProvider _dateTimeProvider;
	private readonly IJobHashConverter _jobHashConverter;
	private readonly IRedisLuaScriptService _luaScriptService;

	public RedisJobStore(
		ILogger<RedisJobStore> logger,
		IDatabase database,
		TimeProvider dateTimeProvider,
		IJobHashConverter jobHashConverter,
		IRedisLuaScriptService luaScriptService)
	{
		_logger = logger;
		_database = database;
		_dateTimeProvider = dateTimeProvider;
		_jobHashConverter = jobHashConverter;
		_luaScriptService = luaScriptService;
	}

	public async Task<Guid> EnqueueAsync(JobDescriptor descriptor, CancellationToken ct = default)
	{
		var jobId = Guid.NewGuid();
		var channel = descriptor.Channel ?? "default";
		var partition = descriptor.PartitionKey is not null
			? Math.Abs(descriptor.PartitionKey.GetHashCode(StringComparison.Ordinal)) % 100
			: (int?)null;

		var record = new JobRecord
		{
			JobId = jobId,
			JobName = descriptor.JobName,
			Channel = channel,
			Priority = descriptor.Priority,
			Partition = partition,
			Payload = descriptor.Payload,
			Status = JobStatus.Queued,
			CreatedAt = _dateTimeProvider.GetUtcNow().UtcDateTime,
			Metadata = descriptor.Metadata
		};

		var score = descriptor.Priority * 1_000_000_000_000L + _dateTimeProvider.GetUtcNow().ToUnixTimeSeconds();
		var hashEntries = _jobHashConverter.ConvertToHashEntries(record);

		await _luaScriptService.EnqueueJobAsync(_database, jobId.ToString(), channel, score, hashEntries);
		return jobId;
	}

	public async Task<JobRecord?> DequeueAsync(string channel, IReadOnlySet<int>? partitions, CancellationToken ct = default)
	{
		var nowIso = _dateTimeProvider.GetUtcNow().ToString("O");
		var nowUnix = _dateTimeProvider.GetUtcNow().ToUnixTimeSeconds().ToString();
		var partitionStr = partitions is not null && partitions.Count > 0
			? string.Join(",", partitions)
			: "";

		var result = await _luaScriptService.DequeueJobAsync(_database, channel, nowIso, nowUnix, partitionStr);

		if (result.Length == 0)
			return null;

		var hashEntries = ConvertToHashEntries(result);
		return _jobHashConverter.ConvertFromHashEntries(hashEntries);
	}

	public async Task UpdateStatusAsync(Guid jobId, JobStatus status, string? result = null, CancellationToken ct = default)
	{
		var jobKey = GetJobKey(jobId);
		var exists = await _database.KeyExistsAsync(jobKey);
		if (!exists)
			throw new KeyNotFoundException($"Job {jobId} not found");

		var currentStatusRaw = await _database.HashGetAsync(jobKey, "Status");
		var currentStatus = (JobStatus)int.Parse(currentStatusRaw.ToString());

		if (!IsValidTransition(currentStatus, status))
			throw new InvalidOperationException($"Invalid state transition from {currentStatus} to {status}");

		var batch = new List<HashEntry>
		{
			new("Status", (int)status)
		};

		if (result is not null)
			batch.Add(new HashEntry("Result", result));

		if (status is JobStatus.Completed or JobStatus.Failed or JobStatus.DeadLettered)
		{
			var nowIso = _dateTimeProvider.GetUtcNow().ToString("O");
			batch.Add(new HashEntry("CompletedAt", nowIso));
		}

		await _database.HashSetAsync(jobKey, batch.ToArray());
	}

	public async Task<JobRecord?> GetStatusAsync(Guid jobId, CancellationToken ct = default)
	{
		var jobKey = GetJobKey(jobId);
		var hashEntries = await _database.HashGetAllAsync(jobKey);

		if (hashEntries.Length == 0)
			return null;

		return _jobHashConverter.ConvertFromHashEntries(hashEntries);
	}

	public async Task HeartbeatAsync(Guid jobId, CancellationToken ct = default)
	{
		var nowIso = _dateTimeProvider.GetUtcNow().ToString("O");
		var nowUnix = _dateTimeProvider.GetUtcNow().ToUnixTimeSeconds().ToString();

		await _luaScriptService.HeartbeatJobAsync(_database, jobId.ToString(), nowIso, nowUnix);
	}

	public async Task<int> ReclaimStaleJobsAsync(TimeSpan staleTimeout, CancellationToken ct = default)
	{
		var cutoff = _dateTimeProvider.GetUtcNow().Add(-staleTimeout).ToUnixTimeSeconds();
		var nowIso = _dateTimeProvider.GetUtcNow().ToString("O");
		var nowUnix = _dateTimeProvider.GetUtcNow().ToUnixTimeSeconds().ToString();

		return await _luaScriptService.ReclaimStaleJobsAsync(_database, cutoff, nowIso, nowUnix);
	}

	private static HashEntry[] ConvertToHashEntries(RedisValue[] values)
	{
		var entries = new HashEntry[values.Length / 2];
		for (var i = 0; i < entries.Length; i++)
		{
			entries[i] = new HashEntry(values[i * 2], values[i * 2 + 1]);
		}
		return entries;
	}

	private static string GetJobKey(Guid jobId) => $"ae:job:{jobId}";

	private static bool IsValidTransition(JobStatus from, JobStatus to)
	{
		if (from == to) return true;
		return (from, to) switch
		{
			(JobStatus.Queued, JobStatus.Processing) => true,
			(JobStatus.Processing, JobStatus.Completed) => true,
			(JobStatus.Processing, JobStatus.Failed) => true,
			(JobStatus.Processing, JobStatus.Queued) => true,
			(JobStatus.Failed, JobStatus.Queued) => true,
			(JobStatus.Failed, JobStatus.DeadLettered) => true,
			_ => false
		};
	}
}
