using AsyncEndpoints.Abstractions.Jobs;
using AsyncEndpoints.Worker.Hosting;
using Microsoft.Extensions.Options;

namespace AsyncEndpoints.Worker.Execution;

public sealed class RetryHandler
{
    private readonly WorkerOptions _options;

    public RetryHandler(IOptions<WorkerOptions> options)
    {
        _options = options.Value;
    }

    public bool ShouldRetry(JobRecord record)
    {
        return record.RetryCount < Math.Max(record.MaxRetries, _options.MaxRetries);
    }

    public TimeSpan GetRetryDelay(JobRecord record)
    {
        var baseDelay = _options.RetryDelayBaseSeconds > 0
            ? TimeSpan.FromSeconds(_options.RetryDelayBaseSeconds)
            : TimeSpan.FromSeconds(2);

        var delayMs = baseDelay.TotalMilliseconds * Math.Pow(2, record.RetryCount);
        return TimeSpan.FromMilliseconds(Math.Min(delayMs, TimeSpan.FromHours(1).TotalMilliseconds));
    }
}
