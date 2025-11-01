using System;
using System.Diagnostics;

namespace AsyncEndpoints.Infrastructure.Observability;

public class MetricTimer : IDisposable
{
    private readonly Action<double> _onDispose;
    private readonly Stopwatch _stopwatch;
    private bool _disposed = false;

    private MetricTimer(Action<double> onDispose)
    {
        _onDispose = onDispose;
        _stopwatch = Stopwatch.StartNew();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _stopwatch.Stop();
            var duration = _stopwatch.Elapsed.TotalSeconds;
            _onDispose(duration);
            _disposed = true;
        }
    }

    public static MetricTimer Start(Action<double> onDurationRecorded)
    {
        return new MetricTimer(onDurationRecorded);
    }
}