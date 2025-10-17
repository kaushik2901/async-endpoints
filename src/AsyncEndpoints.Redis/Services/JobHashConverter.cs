using System.Globalization;
using AsyncEndpoints.Infrastructure.Serialization;
using AsyncEndpoints.JobProcessing;
using AsyncEndpoints.Utilities;
using StackExchange.Redis;

namespace AsyncEndpoints.Redis.Services;

/// <summary>
/// Provides functionality to convert between Job objects and Redis hash entries.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="JobHashConverter"/> class.
/// </remarks>
/// <param name="serializer">The serializer service.</param>
public class JobHashConverter(ISerializer serializer) : IJobHashConverter
{
	private readonly ISerializer _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

	/// <inheritdoc />
	public HashEntry[] ConvertToHashEntries(Job job)
	{
		return
		[
			new HashEntry(nameof(Job.Id), job.Id.ToString()),
			new HashEntry(nameof(Job.Name), job.Name),
			new HashEntry(nameof(Job.Status), (int)job.Status),
			new HashEntry(nameof(Job.Headers), Serialize(job.Headers)),
			new HashEntry(nameof(Job.RouteParams), Serialize(job.RouteParams)),
			new HashEntry(nameof(Job.QueryParams), Serialize(job.QueryParams)),
			new HashEntry(nameof(Job.Payload), job.Payload),
			new HashEntry(nameof(Job.Result), job.Result ?? ""),
			new HashEntry(nameof(Job.Error), job.Error != null ? Serialize(job.Error) : ""),
			new HashEntry(nameof(Job.RetryCount), job.RetryCount),
			new HashEntry(nameof(Job.MaxRetries), job.MaxRetries),
			new HashEntry(nameof(Job.RetryDelayUntil), job.RetryDelayUntil?.ToString("O") ?? ""),
			new HashEntry(nameof(Job.WorkerId), job.WorkerId?.ToString() ?? ""),
			new HashEntry(nameof(Job.CreatedAt), job.CreatedAt.ToString("O")),
			new HashEntry(nameof(Job.StartedAt), job.StartedAt?.ToString("O") ?? ""),
			new HashEntry(nameof(Job.CompletedAt), job.CompletedAt?.ToString("O") ?? ""),
			new HashEntry(nameof(Job.LastUpdatedAt), job.LastUpdatedAt.ToString("O"))
		];
	}

	/// <inheritdoc />
	public Job ConvertFromHashEntries(HashEntry[] hashEntries)
	{
		var dict = hashEntries.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

		return new Job
		{
			Id = Guid.Parse(dict[nameof(Job.Id)]),
			Name = dict[nameof(Job.Name)],
			Status = (JobStatus)int.Parse(dict[nameof(Job.Status)]),
			Headers = string.IsNullOrEmpty(dict[nameof(Job.Headers)]) ? [] : Deserialize<Dictionary<string, List<string?>>>(dict[nameof(Job.Headers)]) ?? [],
			RouteParams = string.IsNullOrEmpty(dict[nameof(Job.RouteParams)]) ? [] : Deserialize<Dictionary<string, object?>>(dict[nameof(Job.RouteParams)]) ?? [],
			QueryParams = string.IsNullOrEmpty(dict[nameof(Job.QueryParams)]) ? [] : Deserialize<List<KeyValuePair<string, List<string?>>>>(dict[nameof(Job.QueryParams)]) ?? [],
			Payload = dict[nameof(Job.Payload)],
			Result = string.IsNullOrEmpty(dict[nameof(Job.Result)]) ? null : dict[nameof(Job.Result)],
			Error = string.IsNullOrEmpty(dict[nameof(Job.Error)]) ? null : Deserialize<AsyncEndpointError>(dict[nameof(Job.Error)]),
			RetryCount = int.Parse(dict[nameof(Job.RetryCount)]),
			MaxRetries = int.Parse(dict[nameof(Job.MaxRetries)]),
			RetryDelayUntil = string.IsNullOrEmpty(dict[nameof(Job.RetryDelayUntil)]) ? null : DateTime.ParseExact(dict[nameof(Job.RetryDelayUntil)], "O", CultureInfo.InvariantCulture),
			WorkerId = string.IsNullOrEmpty(dict[nameof(Job.WorkerId)]) ? null : Guid.Parse(dict[nameof(Job.WorkerId)]),
			CreatedAt = DateTimeOffset.ParseExact(dict[nameof(Job.CreatedAt)], "O", CultureInfo.InvariantCulture),
			StartedAt = string.IsNullOrEmpty(dict[nameof(Job.StartedAt)]) ? null : DateTimeOffset.ParseExact(dict[nameof(Job.StartedAt)], "O", CultureInfo.InvariantCulture),
			CompletedAt = string.IsNullOrEmpty(dict[nameof(Job.CompletedAt)]) ? null : DateTimeOffset.ParseExact(dict[nameof(Job.CompletedAt)], "O", CultureInfo.InvariantCulture),
			LastUpdatedAt = DateTimeOffset.ParseExact(dict[nameof(Job.LastUpdatedAt)], "O", CultureInfo.InvariantCulture)
		};
	}

	private string Serialize(object obj)
	{
		if (obj == null) return "";
		return _serializer.Serialize(obj);
	}

	private T? Deserialize<T>(string value)
	{
		if (string.IsNullOrEmpty(value)) return default!;
		return _serializer.Deserialize<T>(value);
	}
}
