using System;

namespace AsyncEndpoints.Infrastructure;

public class DateTimeProvider : IDateTimeProvider
{
	public DateTime UtcNow => DateTime.UtcNow;

	public DateTimeOffset DateTimeOffsetNow => DateTimeOffset.UtcNow;
}