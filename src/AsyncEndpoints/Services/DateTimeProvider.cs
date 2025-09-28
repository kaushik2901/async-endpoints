using System;
using AsyncEndpoints.Contracts;

namespace AsyncEndpoints.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;

    public DateTimeOffset DateTimeOffsetNow => DateTimeOffset.UtcNow;
}