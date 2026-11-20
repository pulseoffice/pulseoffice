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
    private DispatcherTimer? _countdown;
    private Action? _restartAfterPause;

    public ShowLoopClock(bool kiosk, int pauseMs, int restartMs)
    {
        _kiosk = kiosk;
        _pauseMs = Math.Max(0, pauseMs);
        _restartMs = Math.Max(0, restartMs);
    }

    public bool Pausing => _pause is not null;

    public bool AllowsKey(Key key) => !_kiosk || key == Key.Escape;

    public bool Restarts => _kiosk && _restartMs > 0;

    public int SecondsLeft(int elapsedMs) => (int)Math.Ceiling(Math.Max(0, _pauseMs - Math.Max(0, elapsedMs)) / 1000.0);

    public void Loop(Action paused, Action restart, Action<int>? remaining = null)
    {
        ArgumentNullException.ThrowIfNull(paused);
        ArgumentNullException.ThrowIfNull(restart);
        if (_pauseMs == 0)
        {
            restart();
            return;
        }

        Stop(ref _pause);
        _restartAfterPause = restart;
        paused();
        _pause = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_pauseMs) };
        _pause.Tick += (_, _) => SkipPause();
        _pause.Start();
        if (remaining is null) return;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        remaining(SecondsLeft(0));
        _countdown = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _countdown.Tick += (_, _) => remaining(SecondsLeft((int)clock.ElapsedMilliseconds));
        _countdown.Start();
    }

    public bool SkipPause()
    {
        if (_restartAfterPause is not { } restart) return false;
        _restartAfterPause = null;
        Stop(ref _pause);
        Stop(ref _countdown);
        restart();
        return true;
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
        _restartAfterPause = null;
        Stop(ref _pause);
        Stop(ref _countdown);
        Stop(ref _idle);
    }

    private void Stop(ref DispatcherTimer? timer)
    {
        timer?.Stop();
        timer = null;
    }
}
