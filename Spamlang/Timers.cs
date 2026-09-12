using System.Diagnostics;

namespace Spamlang;

public class Timers : IDisposable
{
    private readonly List<(string what, TimeSpan dt)> _times = new();
    private readonly Stopwatch _totalSw = new();
    private readonly Stopwatch _sw = new();
    private readonly bool _needReport;

    public Timers(bool needReport)
    {
        _needReport = needReport;
        _totalSw.Start();
    }

    public void RestartTimer()
    {
        _sw.Restart();
    }

    public void FinishTimer(string what)
    {
        TimeSpan dt = _sw.Elapsed;
        _times.Add((what, dt));
    }

    public void ReportTimes()
    {
        Console.WriteLine("------ TIMERS ------");
        foreach ((string what, TimeSpan dt) info in _times)
        {
            Console.WriteLine($"{info.what}: {info.dt.Milliseconds}ms");
        }

        Console.WriteLine($"Total Time: {_totalSw.Elapsed.Milliseconds}ms");
    }

    public void Dispose()
    {
        if (_needReport)
        {
            ReportTimes();
        }
    }
}