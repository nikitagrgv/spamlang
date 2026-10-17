using System.Diagnostics;

namespace Spamlang.Frontend;

public class Timers
{
    private readonly List<(string what, TimeSpan dt)> _times = new();
    private readonly Stopwatch _totalSw = new();
    private readonly Stopwatch _sw = new();

    public Timers()
    {
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

    private void ReportTimes(TextWriter writer)
    {
        if (_times.Count == 0)
        {
            return;
        }

        writer.WriteLine("------ TIMERS ------");
        foreach ((string what, TimeSpan dt) info in _times)
        {
            writer.WriteLine($"{info.what}: {info.dt.Milliseconds}ms");
        }

        writer.WriteLine($"Total Time: {_totalSw.Elapsed.Milliseconds}ms");
    }
}