using System.Diagnostics;

namespace Spamlang.Frontend;

public class Timers : IDisposable
{
    private readonly List<(string what, TimeSpan dt)> _times = new();
    private readonly Stopwatch _totalSw = new();
    private readonly Stopwatch _sw = new();
    private readonly TextWriter? _writer;

    public Timers(TextWriter? writer)
    {
        _writer = writer;
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

    private void ReportTimes()
    {
        if (_writer == null || _times.Count == 0)
        {
            return;
        }

        _writer.WriteLine("------ TIMERS ------");
        foreach ((string what, TimeSpan dt) info in _times)
        {
            _writer.WriteLine($"{info.what}: {info.dt.Milliseconds}ms");
        }

        _writer.WriteLine($"Total Time: {_totalSw.Elapsed.Milliseconds}ms");
    }

    public void Dispose()
    {
        ReportTimes();
    }
}