using System;

namespace AsyncEndpoints.Infrastructure;

/// <summary>
/// Provides access to current date and time values in a testable manner.
/// </summary>
public interface IDateTimeProvider
{
	/// <summary>
	/// Gets the current date and time in UTC.
	/// </summary>
	DateTime UtcNow { get; }

	/// <summary>
	/// Gets the current date and time with offset in UTC.
	/// </summary>
	DateTimeOffset DateTimeOffsetNow { get; }
}
