using System.Diagnostics;

namespace Spamlang.Frontend;

public class Timers
{
    private readonly List<(string what, TimeSpan dt)> _times = new();
    private readonly Stopwatch _totalSw = new();
    private readonly Stopwatch _sw = new();

    public int NumRecorded => _times.Count;
    public TimeSpan TotalTime => _totalSw.Elapsed;
    public (string what, TimeSpan dt) TimerAt(int i) => _times[i];

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
}