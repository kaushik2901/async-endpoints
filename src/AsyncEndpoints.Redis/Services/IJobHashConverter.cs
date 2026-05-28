using AsyncEndpoints.Abstractions.Jobs;
using StackExchange.Redis;

namespace AsyncEndpoints.Redis.Services;

public interface IJobHashConverter
{
	HashEntry[] ConvertToHashEntries(JobRecord record);
	JobRecord ConvertFromHashEntries(HashEntry[] hashEntries);
}
