using System;

namespace AsyncEndpoints.Infrastructure;

/// <inheritdoc />
public class DateTimeProvider : IDateTimeProvider
{
	/// <inheritdoc />
	public DateTime UtcNow => DateTime.UtcNow;

	/// <inheritdoc />
	public DateTimeOffset DateTimeOffsetNow => DateTimeOffset.UtcNow;
}
