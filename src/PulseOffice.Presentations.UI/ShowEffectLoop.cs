using Avalonia.Threading;

namespace PulseOffice.Presentations.UI;

public sealed class ShowEffectLoop
{
    private DispatcherTimer? _timer;

    public bool Running => _timer is not null;

    public void Start(Action<int> frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        Stop();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => frame((int)Math.Min(int.MaxValue, clock.ElapsedMilliseconds));
        _timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
    }
}
