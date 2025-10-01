using System;

namespace AsyncEndpoints.Contracts;

public interface IDateTimeProvider
{
	DateTime UtcNow { get; }
	DateTimeOffset DateTimeOffsetNow { get; }
}