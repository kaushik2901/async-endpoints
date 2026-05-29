using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Core.Configuration;
using AsyncEndpoints.Worker.Execution;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.Worker.UnitTests;

public class RetryHandlerTests
{
	private readonly RetryHandler _handler;

	public RetryHandlerTests()
	{
		var options = new AsyncEndpointsOptions { MaxRetries = 3, RetryDelayBaseSeconds = 2.0 };
		_handler = new RetryHandler(Options.Create(options));
	}

	[Fact]
	public void ShouldRetry_ReturnsTrue_WhenRetryCountLessThanMax()
	{
		var record = new JobRecord { RetryCount = 0, MaxRetries = 3 };
		Assert.True(_handler.ShouldRetry(record));
	}

	[Fact]
	public void ShouldRetry_ReturnsTrue_WhenRetryCountEqualsMax()
	{
		var record = new JobRecord { RetryCount = 2, MaxRetries = 3 };
		Assert.True(_handler.ShouldRetry(record));
	}

	[Fact]
	public void ShouldRetry_ReturnsFalse_WhenRetryCountExceeded()
	{
		var record = new JobRecord { RetryCount = 3, MaxRetries = 3 };
		Assert.False(_handler.ShouldRetry(record));
	}

	[Fact]
	public void ShouldRetry_UsesOptionsMaxRetries_WhenRecordMaxRetriesIsZero()
	{
		var record = new JobRecord { RetryCount = 2, MaxRetries = 0 };
		Assert.True(_handler.ShouldRetry(record));

		var record2 = new JobRecord { RetryCount = 3, MaxRetries = 0 };
		Assert.False(_handler.ShouldRetry(record2));
	}

	[Fact]
	public void GetRetryDelay_ExponentialBackoff()
	{
		var record0 = new JobRecord { RetryCount = 0 };
		var delay0 = _handler.GetRetryDelay(record0);
		Assert.InRange(delay0.TotalMilliseconds, 1900, 2100);

		var record1 = new JobRecord { RetryCount = 1 };
		var delay1 = _handler.GetRetryDelay(record1);
		Assert.InRange(delay1.TotalMilliseconds, 3800, 4200);

		var record2 = new JobRecord { RetryCount = 2 };
		var delay2 = _handler.GetRetryDelay(record2);
		Assert.InRange(delay2.TotalMilliseconds, 7600, 8400);
	}

	[Fact]
	public void GetRetryDelay_CapsAtOneHour()
	{
		var record = new JobRecord { RetryCount = 100 };
		var delay = _handler.GetRetryDelay(record);
		Assert.Equal(TimeSpan.FromHours(1), delay);
	}
}
