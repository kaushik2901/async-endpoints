using AsyncEndpoints.JobProcessing;
using StackExchange.Redis;

namespace AsyncEndpoints.Redis.Services;

/// <summary>
/// Provides functionality to convert between Job objects and Redis hash entries.
/// </summary>
public interface IJobHashConverter
{
	/// <summary>
	/// Converts a Job object to Redis hash entries.
	/// </summary>
	/// <param name="job">The Job object to convert.</param>
	/// <returns>An array of HashEntry objects representing the job data.</returns>
	HashEntry[] ConvertToHashEntries(Job job);

	/// <summary>
	/// Converts Redis hash entries to a Job object.
	/// </summary>
	/// <param name="hashEntries">The hash entries containing job data.</param>
	/// <returns>A Job object created from the hash entries.</returns>
	Job ConvertFromHashEntries(HashEntry[] hashEntries);
}
