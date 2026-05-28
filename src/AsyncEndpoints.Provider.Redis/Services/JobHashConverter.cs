using AsyncEndpoints.Abstractions.Jobs;
using StackExchange.Redis;
using System.Globalization;
using System.Text.Json;

namespace AsyncEndpoints.Provider.Redis.Services;

public class JobHashConverter : IJobHashConverter
{
	public HashEntry[] ConvertToHashEntries(JobRecord record)
	{
		return
		[
			new HashEntry("JobId", record.JobId.ToString()),
			new HashEntry("JobName", record.JobName ?? ""),
			new HashEntry("Channel", record.Channel ?? "default"),
			new HashEntry("Priority", record.Priority),
			new HashEntry("Partition", record.Partition?.ToString() ?? ""),
			new HashEntry("Payload", record.Payload ?? ""),
			new HashEntry("Status", (int)record.Status),
			new HashEntry("RetryCount", record.RetryCount),
			new HashEntry("MaxRetries", record.MaxRetries),
			new HashEntry("CreatedAt", record.CreatedAt.ToString("O")),
			new HashEntry("StartedAt", record.StartedAt?.ToString("O") ?? ""),
			new HashEntry("CompletedAt", record.CompletedAt?.ToString("O") ?? ""),
			new HashEntry("WorkerId", record.WorkerId ?? ""),
			new HashEntry("LastHeartbeat", record.LastHeartbeat?.ToString("O") ?? ""),
			new HashEntry("Result", record.Result ?? ""),
			new HashEntry("ErrorMessage", record.ErrorMessage ?? ""),
			new HashEntry("Metadata", record.Metadata is not null ? JsonSerializer.Serialize(record.Metadata) : "")
		];
	}

	public JobRecord ConvertFromHashEntries(HashEntry[] hashEntries)
	{
		var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var entry in hashEntries)
		{
			dict[entry.Name.ToString()] = entry.Value.ToString();
		}

		return new JobRecord
		{
			JobId = TryParseGuid(dict.GetValueOrDefault("JobId")) ?? Guid.Empty,
			JobName = dict.GetValueOrDefault("JobName") ?? "",
			Channel = dict.GetValueOrDefault("Channel") ?? "default",
			Priority = int.TryParse(dict.GetValueOrDefault("Priority"), out var p) ? p : 0,
			Partition = TryParseInt(dict.GetValueOrDefault("Partition")),
			Payload = dict.GetValueOrDefault("Payload") ?? "",
			Status = (JobStatus)int.Parse(dict.GetValueOrDefault("Status", "100")),
			RetryCount = int.TryParse(dict.GetValueOrDefault("RetryCount"), out var rc) ? rc : 0,
			MaxRetries = int.TryParse(dict.GetValueOrDefault("MaxRetries"), out var mr) ? mr : 3,
			CreatedAt = TryParseDateTime(dict.GetValueOrDefault("CreatedAt")) ?? DateTime.UtcNow,
			StartedAt = TryParseDateTime(dict.GetValueOrDefault("StartedAt")),
			CompletedAt = TryParseDateTime(dict.GetValueOrDefault("CompletedAt")),
			WorkerId = string.IsNullOrEmpty(dict.GetValueOrDefault("WorkerId")) ? null : dict["WorkerId"],
			LastHeartbeat = TryParseDateTime(dict.GetValueOrDefault("LastHeartbeat")),
			Result = string.IsNullOrEmpty(dict.GetValueOrDefault("Result")) ? null : dict["Result"],
			ErrorMessage = string.IsNullOrEmpty(dict.GetValueOrDefault("ErrorMessage")) ? null : dict["ErrorMessage"],
			Metadata = DeserializeMetadata(dict.GetValueOrDefault("Metadata"))
		};
	}

	private static Guid? TryParseGuid(string? value)
	{
		if (string.IsNullOrEmpty(value)) return null;
		if (Guid.TryParse(value, out var guid)) return guid;
		return null;
	}

	private static int? TryParseInt(string? value)
	{
		if (string.IsNullOrEmpty(value)) return null;
		if (int.TryParse(value, out var result)) return result;
		return null;
	}

	private static DateTime? TryParseDateTime(string? value)
	{
		if (string.IsNullOrEmpty(value)) return null;
		if (DateTime.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
			return dt;
		if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt2))
			return dt2;
		return null;
	}

	private static Dictionary<string, string>? DeserializeMetadata(string? value)
	{
		if (string.IsNullOrEmpty(value)) return null;
		try { return JsonSerializer.Deserialize<Dictionary<string, string>>(value); }
		catch { return null; }
	}
}
