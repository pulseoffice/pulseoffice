using Avalonia.Threading;

namespace PulseOffice.UI.Controls.Preview;

public interface IDebounceTimer : IDisposable
{
    bool IsPending { get; }

    void Kick();

    void Flush();
}

public interface IDebounceTimerFactory
{
    IDebounceTimer Create(TimeSpan delay, Action action);
}

public sealed class DispatcherDebounceTimerFactory : IDebounceTimerFactory
{
    public IDebounceTimer Create(TimeSpan delay, Action action) => new DispatcherDebounceTimer(delay, action);

    private sealed class DispatcherDebounceTimer : IDebounceTimer
    {
        private readonly DispatcherTimer _timer;
        private readonly Action _action;

        public DispatcherDebounceTimer(TimeSpan delay, Action action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _timer = new DispatcherTimer { Interval = delay };
            _timer.Tick += (_, _) => Flush();
        }

        public bool IsPending => _timer.IsEnabled;

        public void Kick()
        {
            _timer.Stop();
            _timer.Start();
        }

        public void Flush()
        {
            _timer.Stop();
            _action();
        }

        public void Dispose() => _timer.Stop();
    }
}
