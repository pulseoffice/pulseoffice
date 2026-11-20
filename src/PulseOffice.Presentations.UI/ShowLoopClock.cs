using Avalonia.Input;
using Avalonia.Threading;

namespace PulseOffice.Presentations.UI;

public sealed class ShowLoopClock
{
    private readonly bool _kiosk;
    private readonly int _pauseMs;
    private readonly int _restartMs;
    private DispatcherTimer? _pause;
    private DispatcherTimer? _idle;

    public ShowLoopClock(bool kiosk, int pauseMs, int restartMs)
    {
        _kiosk = kiosk;
        _pauseMs = Math.Max(0, pauseMs);
        _restartMs = Math.Max(0, restartMs);
    }

    public bool Pausing => _pause is not null;

    public bool AllowsKey(Key key) => !_kiosk || key == Key.Escape;

    public bool Restarts => _kiosk && _restartMs > 0;

    public void Loop(Action restart)
    {
        ArgumentNullException.ThrowIfNull(restart);
        if (_pauseMs == 0)
        {
            restart();
            return;
        }

        Stop(ref _pause);
        _pause = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_pauseMs) };
        _pause.Tick += (_, _) =>
        {
            Stop(ref _pause);
            restart();
        };
        _pause.Start();
    }

    public void Touch(Action restart)
    {
        ArgumentNullException.ThrowIfNull(restart);
        if (!Restarts) return;
        Stop(ref _idle);
        _idle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_restartMs) };
        _idle.Tick += (_, _) =>
        {
            Stop(ref _idle);
            restart();
        };
        _idle.Start();
    }

    public void Stop()
    {
        Stop(ref _pause);
        Stop(ref _idle);
    }

    private void Stop(ref DispatcherTimer? timer)
    {
        timer?.Stop();
        timer = null;
    }
}
