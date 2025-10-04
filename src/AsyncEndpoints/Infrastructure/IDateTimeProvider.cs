using System;

namespace AsyncEndpoints.Infrastructure;

public interface IDateTimeProvider
{
	DateTime UtcNow { get; }
	DateTimeOffset DateTimeOffsetNow { get; }
}
