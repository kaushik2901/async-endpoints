using AsyncEndpoints.Abstractions.Jobs;
using StackExchange.Redis;

namespace AsyncEndpoints.Provider.Redis.Services;

public interface IJobHashConverter
{
	HashEntry[] ConvertToHashEntries(JobRecord record);
	JobRecord ConvertFromHashEntries(HashEntry[] hashEntries);
}
