using System;
using System.Diagnostics;

namespace AsyncEndpoints.Infrastructure.Observability;

public class MetricTimer : IDisposable
{
    private readonly Action<double> _onDispose;
    private readonly Stopwatch _stopwatch;

    private MetricTimer(Action<double> onDispose)
    {
        _onDispose = onDispose;
        _stopwatch = Stopwatch.StartNew();
    }

    public void Dispose()
    {
        _stopwatch.Stop();
        var duration = _stopwatch.Elapsed.TotalSeconds;
        _onDispose(duration);
    }

    public static MetricTimer Start(Action<double> onDurationRecorded)
    {
        return new MetricTimer(onDurationRecorded);
    }
}